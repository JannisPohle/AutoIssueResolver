namespace AutoIssueResolver.Application.Abstractions.Models;

/// <summary>
///   Represents a code replacement for a specific file.
/// </summary>
public record Replacement(string NewCode, string FilePath);

/// <summary>
/// The response model for the replacements json schema used for the AI response.
/// </summary>
public record ReplacementResponse(List<Replacement> Replacements);
