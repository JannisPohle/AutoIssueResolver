using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnectors.Base.UnifiedModels;

/// <summary>
/// The response model for the replacements json schema used for the AI response.
/// </summary>
public record ReplacementResponse(List<Replacement> Replacements);