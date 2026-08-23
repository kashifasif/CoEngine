using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<OpenRouterService> _logger;

    public OpenRouterService(Kernel kernel, ILogger<OpenRouterService> logger)
    {
        _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    private ChatHistory BuildChatHistory(string systemPrompt, List<ChatMessageDto> history)
    {
        var chatHistory = new ChatHistory(systemPrompt);

        foreach (var msg in history)
        {
            var content = msg.Content ?? "";
            
            if (msg.Role == "user")
            {
                chatHistory.AddUserMessage(content);
            }
            else if (msg.Role == "assistant")
            {
                chatHistory.AddAssistantMessage(content);
            }

            // Inject user confirmed answers for MCQs
            if (msg.Role == "assistant" && content.Contains("<!-- MCQ_ANSWER_"))
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"<!-- MCQ_ANSWER_(\d+): (.*?) -->");
                if (matches.Count > 0)
                {
                    var userAnswers = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        userAnswers.Add($"Question {m.Groups[1].Value}: {m.Groups[2].Value}");
                    }

                    var injectedUserMsg = $"[USER CONFIRMED ANSWERS & SELECTIONS FOR ABOVE QUESTIONS]:\n- " + string.Join("\n- ", userAnswers);
                    chatHistory.AddUserMessage(injectedUserMsg);
                }
            }
        }
        return chatHistory;
    }

    public async Task<ChatResponseDto> ChatAsync(string systemPrompt, List<ChatMessageDto> history)
    {
        try
        {
            var chatHistory = BuildChatHistory(systemPrompt, history);
            var response = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);
            
            return new ChatResponseDto
            {
                Success = true,
                Reply = CleanText(response.Content ?? "No content returned.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call AI via Semantic Kernel");
            return new ChatResponseDto
            {
                Success = false,
                Reply = $"[AI Error] {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string systemPrompt,
        List<ChatMessageDto> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = BuildChatHistory(systemPrompt, history);
        
        IAsyncEnumerable<StreamingChatMessageContent>? streamingResponse = null;
        string? errorMsg = null;
        try
        {
            streamingResponse = _chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI stream connection exception.");
            errorMsg = $"[HTTP Error 500: AI connection error - {ex.Message}]";
        }

        if (errorMsg != null)
        {
            yield return errorMsg;
            yield break;
        }

        if (streamingResponse != null)
        {
            await foreach (var chunk in streamingResponse.WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    var cleaned = CleanText(chunk.Content);
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        yield return cleaned;
                    }
                }
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

        var historyWithTrigger = history.ToList();
        historyWithTrigger.Add(new ChatMessageDto
        {
            Role = "user",
            Content = "ACTION COMMAND: [Structure & Publish Specification]\n" +
                      "Please analyze the ENTIRE conversation history above from the beginning, as well as ALL previous published version specifications (v1, v2, v3...).\n" +
                      "Synthesize ALL brainstorming ideas, answered Q&A options, and previous version requirements into a SINGLE, exhaustive, production-grade Software Requirements Specification (SRS) JSON document matching the required schema now."
        });

        var chatResult = await ChatAsync(promptText, historyWithTrigger);
        return ParseStructuredResult(chatResult, history, systemPrompt);
    }

    private StructuredSpecResultDto ParseStructuredResult(ChatResponseDto chatResult, List<ChatMessageDto> history, string systemPrompt = "")
    {
        if (!chatResult.Success || string.IsNullOrWhiteSpace(chatResult.Reply))
        {
            return FallbackStructuredSpec(history, systemPrompt);
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
            _logger.LogWarning(ex, "Could not parse JSON output from AI response. Falling back to structured extraction.");
        }

        return FallbackStructuredSpec(history, systemPrompt);
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

    private static StructuredSpecResultDto FallbackStructuredSpec(List<ChatMessageDto> history, string systemPrompt = "")
    {
        var userMessages = history
            .Where(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim())
            .Where(u => !u.StartsWith("Option") && !u.StartsWith("Skip") && !u.StartsWith("ACTION COMMAND"))
            .ToList();

        var title = userMessages.FirstOrDefault() ?? "Master Feature Specification";
        if (title.Length > 60) title = title.Substring(0, 60) + "...";

        var summaryText = userMessages.Any() ? userMessages.First() : "Synthesized feature specification.";
        var extractedCriteria = new List<string>();

        // 1. Extract requirements from chat history
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

        // 2. Also extract requirements from systemPrompt (previous published versions v1, v2...)
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            var promptLines = systemPrompt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in promptLines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("AC-") || trimmed.StartsWith("👤") || trimmed.StartsWith("❓"))
                {
                    var cleanItem = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^[-*\s]+", "").Trim();
                    if (!string.IsNullOrWhiteSpace(cleanItem) && !extractedCriteria.Contains(cleanItem))
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
            AcceptanceCriteria = extractedCriteria.ToList(),
            ScopeTags = new List<string> { "api", "bff", "mfe" }
        };
    }
}
