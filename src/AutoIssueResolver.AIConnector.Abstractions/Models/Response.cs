namespace AutoIssueResolver.AIConnector.Abstractions.Models;

/// <summary>
///   Represents a response from the AI, including code replacements.
/// </summary>
/// <typeparam name="T">The type that should be used to parse the response.</typeparam>
public record Response<T>(string ResponseText, T? ParsedResponse);