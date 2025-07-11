using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Abstractions.Configuration;

public record AiAgentConfiguration
{
  public AIModels Model { get; init; } = AIModels.None;

  public string Token { get; init; } = string.Empty;
}