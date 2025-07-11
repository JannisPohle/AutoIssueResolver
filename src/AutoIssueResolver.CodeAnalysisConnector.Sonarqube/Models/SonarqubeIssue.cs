namespace AutoIssueResolver.CodeAnalysisConnector.Sonarqube.Models;

public record SonarqubeIssueResponse(
  int Total,
  int P,
  int Ps,
  Paging Paging,
  int EffortTotal,
  List<SonarqubeIssue> Issues,
  List<Component> Components);

public record SonarqubeIssue(
  string Key,
  string Rule,
  string Severity,
  string Component,
  string Project,
  int Line,
  string Hash,
  TextRange TextRange,
  List<Flow> Flows,
  string Status,
  string Message,
  string Effort,
  string Debt,
  string Author,
  List<string> Tags,

  // DateTimeOffset CreationDate,
  // DateTimeOffset UpdateDate,
  string Type,
  string Scope,
  bool QuickFixAvailable,
  List<object> MessageFormattings,
  List<object> CodeVariants,
  string CleanCodeAttribute,
  string CleanCodeAttributeCategory,
  List<Impact> Impacts,
  string IssueStatus,
  bool PrioritizedRule);

public record TextRange(
  int StartLine,
  int EndLine,
  int StartOffset,
  int EndOffset);

public record Flow(List<Location> Locations);

public record Location(
  string Component,
  TextRange TextRange,
  string Msg,
  List<object> MsgFormattings);

public record Component(
  string Key,
  bool Enabled,
  string Qualifier,
  string Name,
  string LongName,
  string? Path);