using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;

namespace AutoIssueResolver.CodeAnalysisConnector.Abstractions.Configuration;

public record CodeAnalysisConfiguration
{
  public CodeAnalysisTypes Type { get; set; } = CodeAnalysisTypes.None;
  public string? ProjectKey { get; set; }
}