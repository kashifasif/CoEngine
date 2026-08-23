namespace SpecPlatform.Shared.DTOs;

/// <summary>
/// Result of a single self-review quality check.
/// </summary>
public class SelfReviewCheckDto
{
    public bool Passed { get; set; } = true;
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Full result of the self-review run against a structured spec JSON.
/// Stored as JSON alongside the SpecVersion for audit purposes.
/// </summary>
public class SelfReviewResultDto
{
    /// <summary>True only when ALL four checks pass.</summary>
    public bool Passed { get; set; } = true;

    public SelfReviewChecksDto Checks { get; set; } = new();

    /// <summary>
    /// List of auto-fix descriptions applied by the LLM (low-risk only).
    /// Empty if no fixes were made.
    /// </summary>
    public List<string> AutoFixes { get; set; } = new();

    /// <summary>
    /// The (possibly auto-fixed) spec — identical to the input spec if no fixes were applied.
    /// Null if the self-review call failed and could not be parsed.
    /// </summary>
    public StructuredSpecResultDto? RevisedSpec { get; set; }

    /// <summary>Indicates whether the result came from a successful LLM parse or a fallback.</summary>
    public bool IsFallback { get; set; } = false;
}

/// <summary>Results of each of the four independent quality checks.</summary>
public class SelfReviewChecksDto
{
    public SelfReviewCheckDto PlaceholderScan { get; set; } = new();
    public SelfReviewCheckDto InternalConsistency { get; set; } = new();
    public SelfReviewCheckDto ScopeCheck { get; set; } = new();
    public SelfReviewCheckDto AmbiguityCheck { get; set; } = new();
}
