namespace SpecPlatform.Shared.DTOs;

public class UserAiUsageSummaryDto
{
    public int TotalTokens { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalRequests { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public List<AiUsageByOperationDto> BreakdownByOperation { get; set; } = new();
    public List<UserAiUsageRecordDto> RecentLogs { get; set; } = new();
}

public class AiUsageByOperationDto
{
    public string Operation { get; set; } = string.Empty;
    public int TotalTokens { get; set; }
    public int RequestsCount { get; set; }
    public decimal Percentage { get; set; }
}

public class UserAiUsageRecordDto
{
    public int Id { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTime CreatedAt { get; set; }
}
