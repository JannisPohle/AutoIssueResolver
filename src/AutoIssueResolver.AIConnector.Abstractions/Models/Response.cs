namespace AutoIssueResolver.AIConnector.Abstractions.Models;

public record Response(string ResponseText, Replacement CodeReplacement);

public record Replacement(string NewCode, int StartingLine, int EndLine);