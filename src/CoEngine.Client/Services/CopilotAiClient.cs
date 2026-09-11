using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoEngine.Shared.DTOs;
using CoEngine.Shared.Helpers;
using Microsoft.JSInterop;

namespace CoEngine.Client.Services;

public class CopilotAiClient
{
    private readonly HttpClient _copilotHttp;
    private readonly IJSRuntime _js;
    private bool? _isAvailableCached;
    private DateTime _lastAvailabilityCheck = DateTime.MinValue;

    public CopilotAiClient(IJSRuntime js)
    {
        _js = js;
        _copilotHttp = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:5123/"),
            Timeout = TimeSpan.FromMinutes(3)
        };
    }

    public async Task<bool> IsCopilotAvailableAsync(bool forceCheck = false)
    {
        if (!forceCheck && _isAvailableCached.HasValue && (DateTime.UtcNow - _lastAvailabilityCheck).TotalSeconds < 30)
        {
            return _isAvailableCached.Value;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
            var response = await _copilotHttp.GetAsync("v1/models", cts.Token);
            _isAvailableCached = response.IsSuccessStatusCode;
            _lastAvailabilityCheck = DateTime.UtcNow;
            return _isAvailableCached.Value;
        }
        catch
        {
            _isAvailableCached = false;
            _lastAvailabilityCheck = DateTime.UtcNow;
            return false;
        }
    }

    public async Task<bool> StreamChatCompletionAsync(
        string systemPrompt,
        List<ChatMessageDto> messages,
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formattedMessages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                formattedMessages.Add(new { role = "system", content = systemPrompt });
            }

            foreach (var m in messages)
            {
                formattedMessages.Add(new { role = m.Role, content = m.Content });
            }

            var requestPayload = new
            {
                model = "gpt-4o",
                stream = true,
                messages = formattedMessages
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = JsonContent.Create(requestPayload)
            };

            using var response = await _copilotHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                        choices.GetArrayLength() > 0 &&
                        choices[0].TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var contentElem))
                    {
                        var text = contentElem.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            onChunkReceived(text);
                        }
                    }
                }
                catch { }
            }

            return true;
        }
        catch (Exception ex)
        {
            await _js.InvokeVoidAsync("console.warn", $"[CopilotAiClient] Stream error: {ex.Message}");
            return false;
        }
    }

    public async Task<string?> CompleteChatAsync(
        string systemPrompt,
        List<ChatMessageDto> messages,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formattedMessages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                formattedMessages.Add(new { role = "system", content = systemPrompt });
            }

            foreach (var m in messages)
            {
                formattedMessages.Add(new { role = m.Role, content = m.Content });
            }

            var requestPayload = new
            {
                model = "gpt-4o",
                stream = false,
                messages = formattedMessages
            };

            var response = await _copilotHttp.PostAsJsonAsync("v1/chat/completions", requestPayload, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var contentElem))
            {
                return contentElem.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            await _js.InvokeVoidAsync("console.warn", $"[CopilotAiClient] Complete error: {ex.Message}");
            return null;
        }
    }

    public async Task<StructuredSpecResultDto?> StructureSpecAsync(
        string structurePrompt,
        List<ChatMessageDto> messages,
        string selfReviewPrompt,
        CancellationToken cancellationToken = default)
    {
        var messagesWithTrigger = messages.ToList();
        messagesWithTrigger.Add(new ChatMessageDto
        {
            Role = "user",
            Content = "ACTION COMMAND: [Structure & Publish Specification]\n" +
                      "Please analyze the ENTIRE conversation history above from the beginning, as well as ALL previous published version specifications (v1, v2, v3...).\n" +
                      "Synthesize ALL brainstorming ideas, answered Q&A options, and previous version requirements into a SINGLE, exhaustive, production-grade Software Requirements Specification (SRS) JSON document matching the required schema now."
        });

        var replyText = await CompleteChatAsync(structurePrompt, messagesWithTrigger, cancellationToken);
        if (string.IsNullOrWhiteSpace(replyText)) return null;

        var structured = SpecJsonParser.ParseStructuredSpec(replyText);
        if (structured == null) return null;

        if (!string.IsNullOrWhiteSpace(structured.RawJson) && !string.IsNullOrWhiteSpace(selfReviewPrompt))
        {
            structured.SelfReviewResult = await RunSelfReviewAsync(selfReviewPrompt, structured.RawJson, cancellationToken);
            if (structured.SelfReviewResult?.RevisedSpec != null && structured.SelfReviewResult.AutoFixes.Any())
            {
                var revised = structured.SelfReviewResult.RevisedSpec;
                structured.Title = !string.IsNullOrWhiteSpace(revised.Title) ? revised.Title : structured.Title;
                structured.Description = !string.IsNullOrWhiteSpace(revised.Description) ? revised.Description : structured.Description;
                structured.AcceptanceCriteria = revised.AcceptanceCriteria.Any() ? revised.AcceptanceCriteria : structured.AcceptanceCriteria;
                structured.ScopeTags = revised.ScopeTags.Any() ? revised.ScopeTags : structured.ScopeTags;
            }
        }

        return structured;
    }

    public async Task<SelfReviewResultDto> RunSelfReviewAsync(
        string selfReviewPrompt,
        string rawSpecJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reviewMessages = new List<ChatMessageDto>
            {
                new() { Role = "user", Content = $"Here is the structured spec JSON to review:\n\n{rawSpecJson}" }
            };

            var reply = await CompleteChatAsync(selfReviewPrompt, reviewMessages, cancellationToken);
            if (string.IsNullOrWhiteSpace(reply))
            {
                return new SelfReviewResultDto { Passed = true, IsFallback = true };
            }

            return SpecJsonParser.ParseSelfReview(reply);
        }
        catch
        {
            return new SelfReviewResultDto { Passed = true, IsFallback = true };
        }
    }
}
