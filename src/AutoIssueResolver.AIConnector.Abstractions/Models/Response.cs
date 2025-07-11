namespace AutoIssueResolver.AIConnector.Abstractions.Models;

public record Response(string ResponseText, List<Replacement> CodeReplacement);

public record Replacement(string NewCode, string FileName);