namespace CoEngine.Shared.DTOs;

public class ChatMessageDto
{
    public string Role { get; set; } = "user"; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public string? AttachedFileName { get; set; }
    public string? AttachedFileUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? SenderUserId { get; set; }
    public string? SenderName { get; set; }
}

public class ChatRequestDto
{
    public Guid ProjectId { get; set; }
    public bool IsClarificationPhase { get; set; } = false;
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class DevQaQueryRequestDto
{
    public Guid ProjectId { get; set; }
    public string RoleMode { get; set; } = "qa_mode"; // qa_mode
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class DevQaContextResponseDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDescription { get; set; } = string.Empty;
    public string GroundedContext { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<VectorMatchDto> VectorMatches { get; set; } = new();
}

public class BrainstormContextResponseDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDescription { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public int ClarificationRoundNumber { get; set; }
    public bool IsClarificationPhase { get; set; }
    public List<VectorMatchDto> VectorMatches { get; set; } = new();
}

public class VectorMatchDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string DocType { get; set; } = string.Empty;
}

public class ChatSessionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string PersonaMode { get; set; } = "po_brainstorming";
    public DateTime UpdatedAt { get; set; }
    public int TotalMessagesCount { get; set; }
    public bool HasMore { get; set; }
    public int ClarificationRoundNumber { get; set; }
    public int MaxClarificationRounds { get; set; } = 3;
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class AppendMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class StructuredSpecResultDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ScopeTags { get; set; } = new();

    /// <summary>
    /// Raw JSON string produced by the Phase-3 LLM call.
    /// Set server-side only; used as input to the self-review prompt.
    /// Not displayed in the UI.
    /// </summary>
    public string? RawJson { get; set; }

    /// <summary>
    /// Self-review result produced immediately after Phase 3.
    /// Null only if the feature is disabled or a catastrophic error occurred.
    /// </summary>
    public SelfReviewResultDto? SelfReviewResult { get; set; }
}


public class IngestTranscriptRequestDto
{
    public Guid ProjectId { get; set; }
    public string SourceTag { get; set; } = "MS Teams Meeting Transcript";
    public string RawTranscript { get; set; } = string.Empty;
    public StructuredSpecResultDto? PreStructuredResult { get; set; }
}

public class IngestTranscriptResponseDto
{
    public bool Success { get; set; } = true;
    public string SummaryReply { get; set; } = string.Empty;
    public string ExtractedTitle { get; set; } = string.Empty;
    public string ExtractedDescription { get; set; } = string.Empty;
    public List<string> ExtractedCriteria { get; set; } = new();
    public List<string> ExtractedTags { get; set; } = new();
}

