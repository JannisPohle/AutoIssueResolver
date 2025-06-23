using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;

namespace AutoIssueResolver.CodeAnalysisConnector.Abstractions;

public interface ICodeAnalysisConnector
{
  Task<List<Issue>> GetIssues(Project project, CancellationToken cancellationToken = default);

  Task<Rule> GetRule(RuleIdentifier identifier, CancellationToken cancellationToken = default);
}