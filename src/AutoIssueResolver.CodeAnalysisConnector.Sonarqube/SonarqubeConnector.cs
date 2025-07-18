using System.Net;
using System.Net.Http.Json;
using System.Text;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions;
using AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models;
using AutoIssueResolver.CodeAnalysisConnector.Sonarqube.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TextRange = AutoIssueResolver.CodeAnalysisConnector.Abstractions.Models.TextRange;

namespace AutoIssueResolver.CodeAnalysisConnector.Sonarqube;

public class SonarqubeConnector([FromKeyedServices("sonarqube")] HttpClient httpClient, ILogger<SonarqubeConnector> logger): ICodeAnalysisConnector
{
  #region Static

  private const string API_PATH_ISSUES = "api/issues/search";
  private const string API_PATH_RULES = "api/rules/search";

  #endregion

  #region Methods

  public async Task<List<Issue>> GetIssues(Project project, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Requesting issues for project {ProjectName} ({Language}) from Sonarqube", project.ProjectName, project.Language);
    var response = await httpClient.GetAsync($"{API_PATH_ISSUES}?components={project.ProjectName}&languages={project.Language}&resolved=no", cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      logger.LogError("Failed to get issues from Sonarqube: {ReasonPhrase}", response.ReasonPhrase);
      throw new Exception($"Failed to get issues from Sonarqube: {response.ReasonPhrase}");
    }

    var issues = await response.Content.ReadFromJsonAsync<SonarqubeIssueResponse>(cancellationToken);

    if (issues == null)
    {
      logger.LogError("Failed to deserialize issues response from Sonarqube");
      throw new Exception("Failed to deserialize issues response from Sonarqube");
    }

    logger.LogInformation("Retrieved {IssueCount} issues from Sonarqube", issues.Issues.Count);

    var mappedIssues = issues.Issues.Select(issue =>
      new Issue(
        new RuleIdentifier(issue.Rule),
        issues.Components.First(component => component.Key == issue.Component).Path,
        new TextRange(issue.TextRange.StartLine, issue.TextRange.EndLine)
      )).ToList();

    logger.LogDebug("Mapped {Count} issues to internal model", mappedIssues.Count);

    return mappedIssues;
  }

  public async Task<Rule> GetRule(RuleIdentifier identifier, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Requesting rule {RuleId} from Sonarqube", identifier.RuleId);
    var response = await httpClient.GetAsync($"{API_PATH_RULES}?rule_key={identifier.RuleId}", cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      logger.LogError("Failed to get rule from Sonarqube: {ReasonPhrase}", response.ReasonPhrase);
      throw new Exception($"Failed to get rule from Sonarqube: {response.ReasonPhrase}");
    }

    var rules = await response.Content.ReadFromJsonAsync<SonarqubeRuleResponse>(cancellationToken);

    if (rules == null)
    {
      logger.LogError("Failed to deserialize rule response from Sonarqube");
      throw new Exception("Failed to deserialize rule response from Sonarqube");
    }

    var rule = rules.Rules.FirstOrDefault(r => r.Key == identifier.RuleId);

    if (rule == null)
    {
      logger.LogWarning("Rule with ID {RuleId} not found in Sonarqube", identifier.RuleId);
      throw new Exception($"Rule with ID {identifier.RuleId} not found in Sonarqube");
    }

    logger.LogDebug("Retrieved rule {RuleId}: {RuleName}", rule.Key, rule.Name);

    var description = GetDescription(rule);

    return new Rule(rule.Key, rule.Name, description);
  }

  private string GetDescription(SonarqubeRule rule)
  {
    var sb = new StringBuilder();

    if (!string.IsNullOrWhiteSpace(rule.MdDesc) && (string.IsNullOrWhiteSpace(rule.HtmlDesc) || (rule.HtmlDesc.Length < rule.MdDesc.Length)))
    {
      sb.Append(rule.MdDesc);
    }
    else if (!string.IsNullOrWhiteSpace(rule.HtmlDesc))
    {
      // Fallback to HTML description if Markdown is not available or shorter
      sb.Append(RemoveHtmlTags(rule.HtmlDesc));
    }

    foreach (var section in rule.DescriptionSections ?? [])
    {
      if (section.Key == "resources")
      {
        // Skip resources section as it only contains links that cannot be used by the model
        continue;
      }

      var title = section.Key switch
      {
        "root_cause" => "Root Cause",
        "how_to_fix" => "How to Fix",
        "resources" => "Resources",
        "introduction" => "Introduction",
        _ => section.Key
      };

      if (!string.IsNullOrWhiteSpace(section.Content))
      {
        sb.AppendLine();
        sb.AppendLine($"## {title}");
        sb.AppendLine(RemoveHtmlTags(section.Content));
      }
    }

    var description = sb.ToString();
    logger.LogDebug("Constructed description for rule {RuleId}: {DescriptionLength} characters", rule.Key, description.Length);
    logger.LogTrace("Full constructed description: {Description}", description);

    return description;
  }

  private string RemoveHtmlTags(string input)
  {
    var htmlDoc = new HtmlDocument();
    htmlDoc.LoadHtml(input);

    return WebUtility.HtmlDecode(htmlDoc.DocumentNode.InnerText);
  }

  #endregion
}
