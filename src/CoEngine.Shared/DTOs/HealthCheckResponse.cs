namespace CoEngine.Shared.DTOs;

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public string Message { get; set; } = "API is alive";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Environment { get; set; } = "Development";
}
