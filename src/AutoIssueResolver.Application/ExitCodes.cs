namespace AutoIssueResolver.Application;

public enum ExitCodes
{
  ConfigurationError = 1,
  SourceCodeConnectionError = 2,
  CodeAnalysisError = 3,
  AiConnectorError = 4,
  UnknownError = 5,
}