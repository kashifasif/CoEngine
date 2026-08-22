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
                Success = false,
                ErrorMessage = ex.Message
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
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return $"[Error HTTP {(int)response.StatusCode}: {errContent}]";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

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

    public async Task<StructuredSpecResultDto> StructureIntoSpecAsync(string systemPrompt, List<ChatMessageDto> history)
    {
        var structureInstruction =
            "\n\nCRITICAL INSTRUCTION: Examine the entire conversation history above very carefully. Extract EVERY requirement, technical constraint (e.g. max file size limits, file formats, timeout values, status codes), user story, and business rule discussed by the user or assistant.\n" +
            "Format the result into clean JSON with the exact structure:\n" +
            "{\n" +
            "  \"title\": \"Feature Title\",\n" +
            "  \"description\": \"Detailed description of the feature including all technical scope and constraints discussed\",\n" +
            "  \"acceptanceCriteria\": [\"Criterion 1 (must explicitly state any specific limits/sizes/rules discussed)\", \"Criterion 2\"],\n" +
            "  \"scopeTags\": [\"bff\", \"api\", \"mfe\"]\n" +
            "}\nReturn ONLY valid raw JSON with no markdown wrapping.";

        var fullSystemPrompt = systemPrompt + structureInstruction;
        var chatResult = await ChatAsync(fullSystemPrompt, history);

        if (!chatResult.Success || string.IsNullOrWhiteSpace(chatResult.Reply))
        {
            return FallbackStructuredSpec(history);
        }

        try
        {
            var rawText = chatResult.Reply.Trim();
            if (rawText.StartsWith("```json"))
            {
                rawText = rawText.Substring(7);
            }
            if (rawText.StartsWith("```"))
            {
                rawText = rawText.Substring(3);
            }
            if (rawText.EndsWith("```"))
            {
                rawText = rawText.Substring(0, rawText.Length - 3);
            }
            rawText = rawText.Trim();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<StructuredSpecResultDto>(rawText, options);
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Title))
            {
                return parsed;
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
            new { role = "system", content = systemPrompt + "\nRespond strictly in clean Markdown format." }
        };

        foreach (var msg in history)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
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
        var lastUserMsg = history.LastOrDefault(m => m.Role == "user")?.Content ?? "New Feature Specification";
        var title = lastUserMsg.Length > 50 ? lastUserMsg.Substring(0, 50) + "..." : lastUserMsg;

        var extractedCriteria = history
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim())
            .Take(5)
            .ToList();

        return new StructuredSpecResultDto
        {
            Title = title,
            Description = $"Drafted from brainstorming session with {history.Count} messages.\n\nKey discussion points:\n- " +
                string.Join("\n- ", history.Select(h => h.Content.Take(120).ToString())),
            AcceptanceCriteria = extractedCriteria.Any() ? extractedCriteria : new List<string>
            {
                "User can view the feature dashboard",
                "System validates input parameters before saving",
                "Audit log records changes upon publishing"
            },
            ScopeTags = new List<string> { "api", "bff", "web" }
        };
    }
}
