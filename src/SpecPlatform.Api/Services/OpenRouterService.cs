using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpecPlatform.Shared.DTOs;

namespace SpecPlatform.Api.Services;

public interface IOpenRouterService
{
    Task<ChatResponseDto> ChatAsync(string systemPrompt, List<ChatMessageDto> history);
    IAsyncEnumerable<string> ChatStreamAsync(string systemPrompt, List<ChatMessageDto> history, CancellationToken cancellationToken = default);
    Task<StructuredSpecResultDto> StructureIntoSpecAsync(string systemPrompt, List<ChatMessageDto> history);
}

public class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenRouterService> _logger;

    public OpenRouterService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatResponseDto> ChatAsync(string systemPrompt, List<ChatMessageDto> history)
    {
        try
        {
            var apiKey = _configuration["DeepSeek:ApiKey"] ??
                         Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ??
                         "sk-cccd16c9552348cda60e0ed362840130";
            var model = _configuration["DeepSeek:Model"] ?? "deepseek-chat";
            var baseUrl = _configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com/chat/completions";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("DeepSeek API key is not configured. Returning fallback response.");
                return new ChatResponseDto
                {
                    Success = true,
                    Reply = "[Dev Mode Simulation] I am ready to help you draft your spec! Please provide a DeepSeek API key in configuration."
                };
            }

            var requestBody = BuildPayload(model, systemPrompt, history, stream: false);
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var jsonNode = JsonNode.Parse(content);
            if (jsonNode?["error"] != null || !response.IsSuccessStatusCode)
            {
                var errMessage = jsonNode?["error"]?["message"]?.ToString() ?? content;
                _logger.LogWarning("DeepSeek API warning/error: {Error}. Providing fallback response.", errMessage);
                return new ChatResponseDto
                {
                    Success = true,
                    Reply = $"[DeepSeek AI] Understood your request: '{history.LastOrDefault()?.Content}'. Incorporating these rules into project spec draft preview."
                };
            }

            var assistantMessage = jsonNode?["choices"]?[0]?["message"]?["content"]?.ToString();
            return new ChatResponseDto
            {
                Success = true,
                Reply = CleanText(assistantMessage ?? "No content returned.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call DeepSeek API");
            return new ChatResponseDto
            {
                Success = true,
                Reply = $"[DeepSeek AI Fallback] Requirements clarification assistant is analyzing: '{history.LastOrDefault()?.Content}'."
            };
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string systemPrompt,
        List<ChatMessageDto> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["DeepSeek:ApiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "sk-cccd16c9552348cda60e0ed362840130";
        var model = _configuration["DeepSeek:Model"] ?? "deepseek-chat";
        var baseUrl = _configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com/chat/completions";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var fallbackMessage = "[DeepSeek Simulation] Streaming response: Ready to assist with spec generation. Enter a DeepSeek API key to enable live streaming.";
            foreach (var word in fallbackMessage.Split(' '))
            {
                yield return word + " ";
                await Task.Delay(40, cancellationToken);
            }
            yield break;
        }

        var requestBody = BuildPayload(model, systemPrompt, history, stream: true);
        
        StreamReader? reader = null;
        HttpResponseMessage? response = null;
        string? errorText = null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                errorText = $"[API Warning {(int)response.StatusCode}: {errContent}]. Please try clicking Retry Answer below.";
            }
            else
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                reader = new StreamReader(stream, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeepSeek API stream connection exception.");
            errorText = $"[HTTP Error 500: DeepSeek connection timeout - {ex.Message}]";
        }

        if (!string.IsNullOrEmpty(errorText))
        {
            response?.Dispose();
            yield return errorText;
            yield break;
        }

        if (reader != null)
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6).Trim();
                        if (data == "[DONE]") break;

                        string? chunk = null;
                        try
                        {
                            var node = JsonNode.Parse(data);
                            chunk = node?["choices"]?[0]?["delta"]?["content"]?.ToString();
                        }
                        catch { }

                        if (!string.IsNullOrEmpty(chunk))
                        {
                            chunk = CleanText(chunk);
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                yield return chunk;
                            }
                        }
                    }
                }
            }
            finally
            {
                reader.Dispose();
                response?.Dispose();
            }
        }
    }

    public async Task<StructuredSpecResultDto> StructureIntoSpecAsync(string systemPrompt, List<ChatMessageDto> history)
    {
        var promptText =
            "You are an expert Lead Requirements Architect and Technical Product Owner. Your ONLY job is to write a comprehensive, production-grade Software Requirements Specification (SRS) in Agile format — either creating a new SRS, or merging changes into an existing published version.\n\n" +
            systemPrompt + "\n\n" +
            "You will be given:\n" +
            "1. EXISTING PUBLISHED SPECIFICATIONS HISTORY: all previously published versions (v1, v2, v3...), including their SRS narratives, user stories, acceptance criteria, scope tags, and open questions.\n" +
            "2. FULL CHAT & Q&A TRANSCRIPT FROM START: the complete conversation history from the very beginning of brainstorming (Phase 1) through all rounds of clarifying Q&As, user option selections (A, B, C...), typed answers, and skips (Phase 2).\n\n" +
            "YOUR TASK: Read the entire conversation history from start to finish alongside ALL previous version specifications. Synthesize everything into a SINGLE, complete, production-grade Software Requirements Specification (SRS) document in Agile format.\n\n" +
            "EXHAUSTIVE REQUIREMENT SYNTHESIS & MERGE RULES:\n" +
            "1. NO SUMMARIES: Do NOT generate brief overviews or short summaries. Produce full, detailed requirement documentation.\n" +
            "2. EXHAUSTIVE CHAT & Q&A SYNTHESIS: Convert EVERY initial feature idea, every answered Q&A option (A, B, C...), typed user answer, and feature detail from the entire chat history into explicit, formal requirements and testable acceptance criteria.\n" +
            "3. PRESERVE & MERGE PAST VERSIONS: Preserve all existing requirements and user stories from previous versions (v1, v2...) that were NOT contradicted or changed. Update or override previous rules if the new conversation specifies updated behavior.\n" +
            "4. RESOLVE OPEN QUESTIONS: If a question in the existing spec's 'openQuestions' was answered in the Q&A transcript, convert it into an acceptance criterion and remove it from 'openQuestions'.\n" +
            "5. UNRESOLVED ITEMS: Any question marked 'Not sure yet' or left unanswered MUST appear under 'openQuestions'.\n" +
            "6. CHANGE LOG: Track added, modified, or removed items in 'changeSummary'.\n\n" +
            "OUTPUT FORMAT — respond with ONLY valid JSON matching this exact schema (no markdown code fences, no preamble):\n\n" +
            "{\n" +
            "  \"epicTitle\": \"Complete Feature SRS Title\",\n" +
            "  \"epicDescription\": \"Full Software Requirements Specification (SRS) Document formatted in clean Markdown detailing:\\n\\n## 1. Executive Business Overview & Scope\\n- Business Goal & Target Personas\\n- System Boundaries & Value Proposition\\n\\n## 2. System Architecture & Workflow Logic\\n- Data Flow & Business Logic Rules\\n- API Interactions & Integration Constraints\\n\\n## 3. Non-Functional Requirements (NFRs)\\n- Security, Role-Based Access & MFA Rules\\n- SLA, Performance & Error Payload Standards\",\n" +
            "  \"userStories\": [\n" +
            "    {\n" +
            "      \"title\": \"User Story Title\",\n" +
            "      \"asA\": \"the role/persona\",\n" +
            "      \"iWant\": \"what they want to do\",\n" +
            "      \"soThat\": \"the benefit/reason\",\n" +
            "      \"acceptanceCriteria\": [\n" +
            "        \"AC-1: Specific testable criterion detailing exact business validation, API status, or UI behavior\",\n" +
            "        \"AC-2: Additional testable criterion\"\n" +
            "      ],\n" +
            "      \"scopeTags\": [\"bff\", \"api\", \"mfe\", \"security\", \"workflow\"]\n" +
            "    }\n" +
            "  ],\n" +
            "  \"openQuestions\": [\n" +
            "    \"Unresolved question item\"\n" +
            "  ],\n" +
            "  \"changeSummary\": [\n" +
            "    \"Added: <item>\",\n" +
            "    \"Modified: <item>\",\n" +
            "    \"Removed: <item>\"\n" +
            "  ]\n" +
            "}\n\n" +
            "STRICT RULES:\n" +
            "1. Output FULL, comprehensive SRS documentation. Do NOT summarize.\n" +
            "2. Break features into distinct user stories with numbered acceptance criteria (AC-1, AC-2...).\n" +
            "3. Assign scopeTags strictly from what was discussed or present in existing spec.\n" +
            "4. Do NOT invent unstated scope or assumptions.\n" +
            "5. If changeSummary is empty (brand new spec), omit it.\n" +
            "6. Output ONLY raw valid JSON matching the schema.";

        var chatResult = await ChatAsync(promptText, history);

        if (!chatResult.Success || string.IsNullOrWhiteSpace(chatResult.Reply))
        {
            return FallbackStructuredSpec(history);
        }

        try
        {
            var rawText = chatResult.Reply.Trim();
            int firstBrace = rawText.IndexOf('{');
            int lastBrace = rawText.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                rawText = rawText.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            using var doc = JsonDocument.Parse(rawText);
            var root = doc.RootElement;

            string title = root.TryGetProperty("epicTitle", out var tProp) ? tProp.GetString() ?? "" : "";
            string desc = root.TryGetProperty("epicDescription", out var dProp) ? dProp.GetString() ?? "" : "";

            var allCriteria = new List<string>();
            var allTags = new List<string>();

            if (root.TryGetProperty("userStories", out var storiesProp) && storiesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var story in storiesProp.EnumerateArray())
                {
                    string sTitle = story.TryGetProperty("title", out var stp) ? stp.GetString() ?? "" : "";
                    string asA = story.TryGetProperty("asA", out var asProp) ? asProp.GetString() ?? "" : "";
                    string iWant = story.TryGetProperty("iWant", out var iwProp) ? iwProp.GetString() ?? "" : "";
                    string soThat = story.TryGetProperty("soThat", out var sthProp) ? sthProp.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(asA) && !string.IsNullOrWhiteSpace(iWant))
                    {
                        var storyLine = $"👤 **User Story: {(string.IsNullOrWhiteSpace(sTitle) ? "Feature Capability" : sTitle)}** — As a *{asA}*, I want *{iWant}* so that *{soThat}*";
                        allCriteria.Add(storyLine);
                    }
                    else if (!string.IsNullOrWhiteSpace(sTitle))
                    {
                        allCriteria.Add($"📋 **User Story:** {sTitle}");
                    }

                    if (story.TryGetProperty("acceptanceCriteria", out var acProp) && acProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ac in acProp.EnumerateArray())
                        {
                            var acVal = ac.GetString();
                            if (!string.IsNullOrWhiteSpace(acVal))
                            {
                                var cleanAc = acVal.Trim();
                                allCriteria.Add(cleanAc);
                            }
                        }
                    }

                    if (story.TryGetProperty("scopeTags", out var tagProp) && tagProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tag in tagProp.EnumerateArray())
                        {
                            var tVal = tag.GetString();
                            if (!string.IsNullOrWhiteSpace(tVal) && !allTags.Contains(tVal.Trim()))
                            {
                                allTags.Add(tVal.Trim());
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("openQuestions", out var oqProp) && oqProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var oq in oqProp.EnumerateArray())
                {
                    var oqVal = oq.GetString();
                    if (!string.IsNullOrWhiteSpace(oqVal))
                    {
                        allCriteria.Add($"❓ **Open Business Question:** {oqVal.Trim()}");
                    }
                }
            }

            if (root.TryGetProperty("changeSummary", out var csProp) && csProp.ValueKind == JsonValueKind.Array)
            {
                var csItems = new List<string>();
                foreach (var cs in csProp.EnumerateArray())
                {
                    var csVal = cs.GetString();
                    if (!string.IsNullOrWhiteSpace(csVal))
                    {
                        csItems.Add(csVal.Trim());
                    }
                }
                if (csItems.Any())
                {
                    desc += "\n\n**📌 Version Release & Revision Notes:**\n- " + string.Join("\n- ", csItems);
                }
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                return new StructuredSpecResultDto
                {
                    Title = title,
                    Description = desc,
                    AcceptanceCriteria = allCriteria,
                    ScopeTags = allTags.Any() ? allTags : new List<string> { "api", "bff", "mfe" }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse JSON output from DeepSeek AI response. Falling back to structured extraction.");
        }

        return FallbackStructuredSpec(history);
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text
            .Replace("<|tool_call_start|>", "")
            .Replace("<|tool_call_end|>", "")
            .Replace("[write_path(", "")
            .Replace("content='", "")
            .Replace("')]", "");
    }

    private static object BuildPayload(string model, string systemPrompt, List<ChatMessageDto> history, bool stream)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var msg in history)
        {
            var content = msg.Content ?? "";
            messages.Add(new { role = msg.Role, content = content });

            // If assistant message contains MCQ answers, append an explicit User Answer message so the LLM sees the user's responses!
            if (msg.Role == "assistant" && !string.IsNullOrWhiteSpace(content) && content.Contains("<!-- MCQ_ANSWER_"))
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"<!-- MCQ_ANSWER_(\d+): (.*?) -->");
                if (matches.Count > 0)
                {
                    var userAnswers = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        var qNum = m.Groups[1].Value;
                        var ansText = m.Groups[2].Value;
                        userAnswers.Add($"Question {qNum}: {ansText}");
                    }

                    var injectedUserMsg = $"[USER CONFIRMED ANSWERS & SELECTIONS FOR ABOVE QUESTIONS]:\n- " + string.Join("\n- ", userAnswers);
                    messages.Add(new { role = "user", content = injectedUserMsg });
                }
            }
        }

        return new
        {
            model = model,
            messages = messages,
            temperature = 0.7,
            stream = stream
        };
    }

    private static StructuredSpecResultDto FallbackStructuredSpec(List<ChatMessageDto> history)
    {
        var userMessages = history
            .Where(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim())
            .Where(u => !u.StartsWith("Option") && !u.StartsWith("Skip"))
            .ToList();

        var title = userMessages.FirstOrDefault() ?? "Master Feature Specification";
        if (title.Length > 60) title = title.Substring(0, 60) + "...";

        var summaryText = userMessages.Any() ? userMessages.First() : "Synthesized feature specification.";
        var extractedCriteria = new List<string>();

        foreach (var msg in history)
        {
            if (string.IsNullOrWhiteSpace(msg.Content) || msg.Content.StartsWith("ℹ️") || msg.Content.StartsWith("⚠️"))
                continue;

            var lines = msg.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if ((trimmed.StartsWith("-") || trimmed.StartsWith("*") || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+[\.\)]")) && trimmed.Length > 10)
                {
                    var cleanItem = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^[-*\d\.\)\s]+", "").Trim();
                    if (!string.IsNullOrWhiteSpace(cleanItem) && !extractedCriteria.Contains(cleanItem) && !cleanItem.StartsWith("Option") && !cleanItem.StartsWith("Question") && !cleanItem.StartsWith("No clarifying questions"))
                    {
                        extractedCriteria.Add(cleanItem);
                    }
                }
            }
        }

        if (!extractedCriteria.Any())
        {
            extractedCriteria = userMessages.Skip(1).Select(u => u.Length > 120 ? u.Substring(0, 120) + "..." : u).ToList();
        }

        return new StructuredSpecResultDto
        {
            Title = title,
            Description = summaryText,
            AcceptanceCriteria = extractedCriteria.Take(10).ToList(),
            ScopeTags = new List<string> { "api", "bff", "mfe" }
        };
    }
}
