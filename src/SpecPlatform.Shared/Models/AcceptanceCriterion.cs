namespace SpecPlatform.Shared.Models;

public class AcceptanceCriterion
{
    public int Id { get; set; }
    public int SpecVersionId { get; set; }
    public string Text { get; set; } = string.Empty;
}
