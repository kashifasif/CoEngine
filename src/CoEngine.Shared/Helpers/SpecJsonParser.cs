using System.Text.Json;
using CoEngine.Shared.DTOs;

namespace CoEngine.Shared.Helpers;

public static class SpecJsonParser
{
    public static string ExtractRawJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var raw = text.Trim();
        int firstBrace = raw.IndexOf('{');
        int lastBrace = raw.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return raw.Substring(firstBrace, lastBrace - firstBrace + 1);
        return string.Empty;
    }

    public static StructuredSpecResultDto? ParseStructuredSpec(string jsonText)
    {
        var rawText = ExtractRawJson(jsonText);
        if (string.IsNullOrEmpty(rawText)) return null;

        try
        {
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
                                allCriteria.Add(acVal.Trim());
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
                    RawJson = rawText
                };
            }
        }
        catch { }

        return null;
    }

    public static SelfReviewResultDto ParseSelfReview(string jsonText)
    {
        var rawText = ExtractRawJson(jsonText);
        if (string.IsNullOrEmpty(rawText))
        {
            return new SelfReviewResultDto { Passed = true, IsFallback = true };
        }

        try
        {
            using var doc = JsonDocument.Parse(rawText);
            var root = doc.RootElement;

            var checks = new SelfReviewChecksDto
            {
                PlaceholderScan = ParseCheck(root, "placeholderScan"),
                InternalConsistency = ParseCheck(root, "internalConsistency"),
                ScopeCheck = ParseCheck(root, "scopeCheck"),
                AmbiguityCheck = ParseCheck(root, "ambiguityCheck")
            };

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

            StructuredSpecResultDto? revisedSpec = null;
            if (root.TryGetProperty("revisedSpec", out var rsProp) && rsProp.ValueKind == JsonValueKind.Object)
            {
                revisedSpec = ParseStructuredSpec(rsProp.GetRawText());
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
        catch
        {
            return new SelfReviewResultDto { Passed = true, IsFallback = true };
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
}
