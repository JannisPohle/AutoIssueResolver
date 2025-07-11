namespace AutoIssueResolver.AIConnector.Abstractions.Models;

/// <summary>
///   Represents a response from the AI, including code replacements.
/// </summary>
public record Response(string ResponseText, List<Replacement> CodeReplacement);

/// <summary>
///   Represents a code replacement for a specific file.
/// </summary>
public record Replacement(string NewCode, string FileName);