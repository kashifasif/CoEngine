namespace SpecPlatform.Shared.Models;

public class ScopeTag
{
    public Guid Id { get; set; }
    public Guid SpecVersionId { get; set; }
    public string TagName { get; set; } = string.Empty;
}
