using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Abstractions.Configuration;

/// <summary>
///   Configuration for an AI agent, including model and authentication token.
/// </summary>
public record AiAgentConfiguration
{
  #region Properties

  /// <summary>
  ///   The AI model to use.
  /// </summary>
  public AIModels Model { get; init; } = AIModels.None;

  /// <summary>
  ///   The authentication token for the AI service.
  /// </summary>
  public string? Token { get; init; } = string.Empty;

  #endregion
}