namespace CoEngine.Shared.Models;

public class AcceptanceCriterion
{
    public Guid Id { get; set; }
    public Guid SpecVersionId { get; set; }
    public string Text { get; set; } = string.Empty;
}
