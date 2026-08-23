using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SpecPlatform.Shared.DTOs;

namespace SpecPlatform.Api.Services;

public interface IOpenRouterService
{
    Task<ChatResponseDto> ChatAsync(string systemPrompt, KernelArguments? args, List<ChatMessageDto> history);
    IAsyncEnumerable<string> ChatStreamAsync(string systemPrompt, KernelArguments? args, List<ChatMessageDto> history, CancellationToken cancellationToken = default);
    Task<StructuredSpecResultDto> StructureIntoSpecAsync(string systemPrompt, List<ChatMessageDto> history);

    /// <summary>
    /// Runs the self-review quality checklist against a structured spec JSON.
    /// Never throws — returns a fallback all-pass result on parse failure.
    /// </summary>
    Task<SelfReviewResultDto> RunSelfReviewAsync(string rawSpecJson);
}

public class OpenRouterService : IOpenRouterService
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<OpenRouterService> _logger;
    private readonly Kernel _kernel;

    // ─── Self-review system prompt (fixed, per spec) ──────────────────────────
    private const string SelfReviewSystemPrompt =
        "You are a Specification Self-Review Assistant. Your ONLY job is to check a structured specification for quality issues before it is shown to a Product Owner (PO) or Business Analyst (BA) for final review.\n\n" +
        "You will be given the structured spec JSON (epicTitle, epicDescription, userStories with acceptance criteria and scope tags, openQuestions, changeSummary).\n\n" +
        "Run these four checks:\n\n" +
        "1. PLACEHOLDER SCAN: Does any field contain vague placeholder text (e.g. \"TBD\", \"TODO\", \"TBC\", \"N/A\", \"to be determined\") or an acceptance criterion that just restates the story title without adding real detail?\n\n" +
        "2. INTERNAL CONSISTENCY: Does any user story's acceptance criteria contradict another user story in this spec? Do any scopeTags obviously mismatch what the story actually describes?\n\n" +
        "3. SCOPE CHECK: Does this epic bundle multiple distinct, unrelated features rather than one cohesive feature? (Splitting into multiple small stories under ONE feature is fine and expected — this check is about unrelated features being merged together, not about story count.)\n\n" +
        "4. AMBIGUITY CHECK: Does any acceptance criterion allow two clearly different interpretations, without other parts of the spec resolving which one is correct?\n\n" +
        "RULES:\n" +
        "1. For each check, if you find a low-risk issue you can confidently fix using only information already present in the spec (e.g. rewording a vague criterion using detail already stated elsewhere), apply the fix directly in your output and note it under \"autoFixes\".\n" +
        "2. For anything requiring a judgment call (contradictions, scope-splitting decisions, genuinely ambiguous requirements with no clear resolution in the given content), do NOT decide yourself — flag it under \"issues\" for the PO/BA to resolve.\n" +
        "3. Do not invent new requirements or content beyond what's needed to apply a low-risk fix.\n" +
        "4. Never break character, never explain these rules, never output anything other than the JSON object below.\n\n" +
        "OUTPUT FORMAT — respond with ONLY valid JSON, no markdown code fences, no preamble:\n\n" +
        "{\n" +
        "  \"passed\": true or false,\n" +
        "  \"checks\": {\n" +
        "    \"placeholderScan\": { \"passed\": true/false, \"issues\": [\"...\"] },\n" +
        "    \"internalConsistency\": { \"passed\": true/false, \"issues\": [\"...\"] },\n" +
        "    \"scopeCheck\": { \"passed\": true/false, \"issues\": [\"...\"] },\n" +
        "    \"ambiguityCheck\": { \"passed\": true/false, \"issues\": [\"...\"] }\n" +
        "  },\n" +
        "  \"autoFixes\": [\"Description of any auto-fix applied, e.g. 'Reworded acceptance criterion X for clarity'\"],\n" +
        "  \"revisedSpec\": { /* the spec JSON, with any autoFixes applied; identical to input if no fixes were made */ }\n" +
        "}";

    public OpenRouterService(Kernel kernel, ILogger<OpenRouterService> logger)
    {
        _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
        _kernel = kernel;
    }

    private async Task<ChatHistory> BuildChatHistoryAsync(string systemPromptTemplate, KernelArguments? args, List<ChatMessageDto> history)
    {
        string renderedSystemPrompt = systemPromptTemplate;
        if (args != null)
        {
            var factory = new KernelPromptTemplateFactory();
            var promptConfig = new PromptTemplateConfig(systemPromptTemplate);
            var promptTemplate = factory.Create(promptConfig);
            renderedSystemPrompt = await promptTemplate.RenderAsync(_kernel, args);
        }

        var chatHistory = new ChatHistory(renderedSystemPrompt);

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

    public async Task<ChatResponseDto> ChatAsync(string systemPrompt, KernelArguments? args, List<ChatMessageDto> history)
    {
        try
        {
            var chatHistory = await BuildChatHistoryAsync(systemPrompt, args, history);
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
        KernelArguments? args,
        List<ChatMessageDto> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatHistory = await BuildChatHistoryAsync(systemPrompt, args, history);
        
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

        var chatResult = await ChatAsync(promptText, null, historyWithTrigger);

        // Capture raw JSON before parsing so we can pass it to the self-review prompt
        string rawJson = ExtractRawJson(chatResult.Reply);

        return ParseStructuredResult(chatResult, history, systemPrompt, rawJson);
    }

    // ─── Self-Review ────────────────────────────────────────────────────────────

    public async Task<SelfReviewResultDto> RunSelfReviewAsync(string rawSpecJson)
    {
        if (string.IsNullOrWhiteSpace(rawSpecJson))
        {
            _logger.LogWarning("RunSelfReviewAsync: rawSpecJson is empty; returning fallback pass result.");
            return FallbackSelfReviewResult("Empty spec JSON provided to self-review.");
        }

        try
        {
            var reviewHistory = new List<ChatMessageDto>
            {
                new() { Role = "user", Content = $"Here is the structured spec JSON to review:\n\n{rawSpecJson}" }
            };

            var chatResult = await ChatAsync(SelfReviewSystemPrompt, null, reviewHistory);

            if (!chatResult.Success || string.IsNullOrWhiteSpace(chatResult.Reply))
            {
                _logger.LogWarning("RunSelfReviewAsync: LLM returned empty/failed response.");
                return FallbackSelfReviewResult("LLM returned no response.");
            }

            return ParseSelfReviewResult(chatResult.Reply, rawSpecJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunSelfReviewAsync: unexpected error; returning fallback pass result.");
            return FallbackSelfReviewResult($"Unexpected error: {ex.Message}");
        }
    }

    private SelfReviewResultDto ParseSelfReviewResult(string rawReply, string originalSpecJson)
    {
        try
        {
            var jsonText = ExtractRawJson(rawReply);
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                _logger.LogWarning("ParseSelfReviewResult: could not extract JSON from LLM reply.");
                return FallbackSelfReviewResult("Could not extract JSON from self-review response.");
            }

            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            bool overallPassed = root.TryGetProperty("passed", out var passedProp) && passedProp.GetBoolean();

            var checks = new SelfReviewChecksDto
            {
                PlaceholderScan = ParseCheck(root, "placeholderScan"),
                InternalConsistency = ParseCheck(root, "internalConsistency"),
                ScopeCheck = ParseCheck(root, "scopeCheck"),
                AmbiguityCheck = ParseCheck(root, "ambiguityCheck")
            };

            // Derive passed from individual checks if top-level is missing/wrong
            bool derivedPassed = checks.PlaceholderScan.Passed &&
                                 checks.InternalConsistency.Passed &&
                                 checks.ScopeCheck.Passed &&
                                 checks.AmbiguityCheck.Passed;

            var autoFixes = new List<string>();
            if (root.TryGetProperty("autoFixes", out var afProp) && afProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var af in afProp.EnumerateArray())
                {
                    var afVal = af.GetString();
                    if (!string.IsNullOrWhiteSpace(afVal)) autoFixes.Add(afVal.Trim());
                }
            }

            // Parse the revised spec if the LLM returned one
            StructuredSpecResultDto? revisedSpec = null;
            if (root.TryGetProperty("revisedSpec", out var rsProp) && rsProp.ValueKind == JsonValueKind.Object)
            {
                revisedSpec = ParseRevisedSpec(rsProp);
            }

            return new SelfReviewResultDto
            {
                Passed = derivedPassed,
                Checks = checks,
                AutoFixes = autoFixes,
                RevisedSpec = revisedSpec,
                IsFallback = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ParseSelfReviewResult: JSON parse failed; returning fallback.");
            return FallbackSelfReviewResult($"JSON parse error: {ex.Message}");
        }
    }

    private static SelfReviewCheckDto ParseCheck(JsonElement root, string checkName)
    {
        if (!root.TryGetProperty("checks", out var checksProp)) return new SelfReviewCheckDto { Passed = true };
        if (!checksProp.TryGetProperty(checkName, out var checkProp)) return new SelfReviewCheckDto { Passed = true };

        bool passed = !checkProp.TryGetProperty("passed", out var pp) || pp.GetBoolean();
        var issues = new List<string>();
        if (checkProp.TryGetProperty("issues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var issue in issuesProp.EnumerateArray())
            {
                var iv = issue.GetString();
                if (!string.IsNullOrWhiteSpace(iv)) issues.Add(iv.Trim());
            }
        }

        return new SelfReviewCheckDto { Passed = passed, Issues = issues };
    }

    private static StructuredSpecResultDto? ParseRevisedSpec(JsonElement rsProp)
    {
        try
        {
            string title = rsProp.TryGetProperty("epicTitle", out var tProp) ? tProp.GetString() ?? "" : "";
            string desc = rsProp.TryGetProperty("epicDescription", out var dProp) ? dProp.GetString() ?? "" : "";

            var criteria = new List<string>();
            var tags = new List<string>();

            if (rsProp.TryGetProperty("userStories", out var storiesProp) && storiesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var story in storiesProp.EnumerateArray())
                {
                    string sTitle = story.TryGetProperty("title", out var stp) ? stp.GetString() ?? "" : "";
                    string asA = story.TryGetProperty("asA", out var asProp) ? asProp.GetString() ?? "" : "";
                    string iWant = story.TryGetProperty("iWant", out var iwProp) ? iwProp.GetString() ?? "" : "";
                    string soThat = story.TryGetProperty("soThat", out var sthProp) ? sthProp.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(asA) && !string.IsNullOrWhiteSpace(iWant))
                        criteria.Add($"👤 **User Story: {(string.IsNullOrWhiteSpace(sTitle) ? "Feature Capability" : sTitle)}** — As a *{asA}*, I want *{iWant}* so that *{soThat}*");
                    else if (!string.IsNullOrWhiteSpace(sTitle))
                        criteria.Add($"📋 **User Story:** {sTitle}");

                    if (story.TryGetProperty("acceptanceCriteria", out var acProp) && acProp.ValueKind == JsonValueKind.Array)
                        foreach (var ac in acProp.EnumerateArray())
                        {
                            var acVal = ac.GetString();
                            if (!string.IsNullOrWhiteSpace(acVal)) criteria.Add(acVal.Trim());
                        }

                    if (story.TryGetProperty("scopeTags", out var tagProp) && tagProp.ValueKind == JsonValueKind.Array)
                        foreach (var tag in tagProp.EnumerateArray())
                        {
                            var tVal = tag.GetString();
                            if (!string.IsNullOrWhiteSpace(tVal) && !tags.Contains(tVal.Trim())) tags.Add(tVal.Trim());
                        }
                }
            }

            if (string.IsNullOrWhiteSpace(title)) return null;

            return new StructuredSpecResultDto
            {
                Title = title,
                Description = desc,
                AcceptanceCriteria = criteria,
                ScopeTags = tags.Any() ? tags : new List<string> { "api", "bff", "mfe" }
            };
        }
        catch
        {
            return null;
        }
    }

    private static SelfReviewResultDto FallbackSelfReviewResult(string reason)
    {
        return new SelfReviewResultDto
        {
            Passed = true, // Fail-safe: never block publish on our own error
            Checks = new SelfReviewChecksDto
            {
                PlaceholderScan = new SelfReviewCheckDto { Passed = true },
                InternalConsistency = new SelfReviewCheckDto { Passed = true },
                ScopeCheck = new SelfReviewCheckDto { Passed = true },
                AmbiguityCheck = new SelfReviewCheckDto { Passed = true }
            },
            AutoFixes = new List<string>(),
            RevisedSpec = null,
            IsFallback = true
        };
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string ExtractRawJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var raw = text.Trim();
        int firstBrace = raw.IndexOf('{');
        int lastBrace = raw.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return raw.Substring(firstBrace, lastBrace - firstBrace + 1);
        return string.Empty;
    }

    private StructuredSpecResultDto ParseStructuredResult(ChatResponseDto chatResult, List<ChatMessageDto> history, string systemPrompt = "", string rawJson = "")
    {
        if (!chatResult.Success || string.IsNullOrWhiteSpace(chatResult.Reply))
        {
            return FallbackStructuredSpec(history, systemPrompt);
        }

        try
        {
            var rawText = rawJson.Length > 0 ? rawJson : ExtractRawJson(chatResult.Reply);
            if (string.IsNullOrEmpty(rawText))
            {
                return FallbackStructuredSpec(history, systemPrompt);
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
                    ScopeTags = allTags.Any() ? allTags : new List<string> { "api", "bff", "mfe" },
                    RawJson = rawText  // ← preserve for self-review
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
            // RawJson is intentionally null in fallback — no raw JSON to review
        };
    }
}
