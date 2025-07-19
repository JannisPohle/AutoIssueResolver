namespace AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;

public record RuleIdentifier(string RuleId)
{
  public string? ShortIdentifier { get; set; }
};

public record Rule(string RuleId, string Title, string Description): RuleIdentifier(RuleId);