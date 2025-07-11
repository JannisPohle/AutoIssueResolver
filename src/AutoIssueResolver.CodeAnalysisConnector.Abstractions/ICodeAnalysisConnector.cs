using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;

namespace AutoIssueResolver.CodeAnalysisConnector.Abstractions;

public interface ICodeAnalysisConnector
{
  #region Methods

  Task<List<Issue>> GetIssues(Project project, CancellationToken cancellationToken = default);

  Task<Rule> GetRule(RuleIdentifier identifier, CancellationToken cancellationToken = default);

  #endregion
}