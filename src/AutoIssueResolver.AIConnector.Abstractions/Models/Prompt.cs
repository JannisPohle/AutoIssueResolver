namespace AutoIssueResolver.AIConnector.Abstractions.Models;

/// <summary>
///   Represents a prompt to be sent to the AI.
/// </summary>
public record Prompt(string PromptText, string RuleId);