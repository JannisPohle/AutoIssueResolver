using System.Net.Http.Json;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;
using AutoIssueResolver.CodeAnalysisConnector.Sonarqube.Models;
using Microsoft.Extensions.DependencyInjection;
using TextRange = AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models.TextRange;

namespace AutoIssueResolver.CodeAnalysisConnector.Sonarqube;

public class SonarqubeConnector([FromKeyedServices("sonarqube")] HttpClient httpClient): ICodeAnalysisConnector
{
  #region Static

  private const string API_PATH_ISSUES = "api/issues/search";
  private const string API_PATH_RULES = "api/rules/search";

  #endregion

  #region Methods

  public async Task<List<Issue>> GetIssues(Project project, CancellationToken cancellationToken = default)
  {
    var response = await httpClient.GetAsync($"{API_PATH_ISSUES}?components={project.ProjectName}&languages={project.Language}", cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new Exception($"Failed to get issues from Sonarqube: {response.ReasonPhrase}");
    }

    var issues = await response.Content.ReadFromJsonAsync<SonarqubeIssueResponse>(cancellationToken);

    if (issues == null)
    {
      throw new Exception("Failed to deserialize issues response from Sonarqube");
    }

    return issues.Issues.Select(issue => new Issue(new RuleIdentifier(issue.Rule), issues.Components.First(component => component.Key == issue.Component).Path, new TextRange(issue.TextRange.StartLine, issue.TextRange.EndLine))).ToList();
  }

  public async Task<Rule> GetRule(RuleIdentifier identifier, CancellationToken cancellationToken = default)
  {
    var response = await httpClient.GetAsync($"{API_PATH_RULES}?rule_key={identifier.RuleId}", cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new Exception($"Failed to get rule from Sonarqube: {response.ReasonPhrase}");
    }

    var rules = await response.Content.ReadFromJsonAsync<SonarqubeRuleResponse>(cancellationToken);

    if (rules == null)
    {
      throw new Exception("Failed to deserialize rule response from Sonarqube");
    }

    var rule = rules.Rules.FirstOrDefault(r => r.Key == identifier.RuleId);

    if (rule == null)
    {
      throw new Exception($"Rule with ID {identifier.RuleId} not found in Sonarqube");
    }

    return new Rule(rule.Key, rule.Name, rule.HtmlDesc);
  }

  #endregion
}