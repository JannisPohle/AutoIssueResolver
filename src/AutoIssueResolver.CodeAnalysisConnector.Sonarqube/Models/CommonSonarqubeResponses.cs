namespace AutoIssueResolver.CodeAnalysisConnector.Sonarqube.Models;

public record Impact(
  string SoftwareQuality,
  string Severity
);

public record Paging(
  int PageIndex,
  int PageSize,
  int Total
);