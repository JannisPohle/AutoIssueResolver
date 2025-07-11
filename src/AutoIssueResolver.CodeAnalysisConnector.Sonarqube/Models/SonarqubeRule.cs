namespace AutoIssueResolver.CodeAnalysisConnector.Sonarqube.Models;

public record SonarqubeRuleResponse(
  int Total,
  int P,
  int Ps,
  List<SonarqubeRule> Rules,
  Paging Paging);

public record SonarqubeRule(
  string Key,
  string Repo,
  string Name,

  // DateTime CreatedAt,
  string HtmlDesc,
  string MdDesc,
  string Severity,
  string Status,
  bool IsTemplate,
  List<string> Tags,
  List<string> SysTags,
  string Lang,
  string LangName,
  List<object> Params,
  string DefaultDebtRemFnType,
  string DebtRemFnType,
  string Type,
  string DefaultRemFnType,
  string DefaultRemFnBaseEffort,
  string RemFnType,
  string RemFnBaseEffort,
  bool RemFnOverloaded,
  string Scope,
  bool IsExternal,
  List<DescriptionSection> DescriptionSections,
  List<object> EducationPrinciples,

  // DateTimeOffset UpdatedAt,
  string CleanCodeAttribute,
  string CleanCodeAttributeCategory,
  List<Impact> Impacts);

public record DescriptionSection(
  string Key,
  string Content);