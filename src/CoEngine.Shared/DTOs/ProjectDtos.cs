namespace SpecPlatform.Shared.DTOs;

public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int SpecCount { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public string? CreatedByUsername { get; set; }
    public string? CreatedByAvatarUrl { get; set; }
}
