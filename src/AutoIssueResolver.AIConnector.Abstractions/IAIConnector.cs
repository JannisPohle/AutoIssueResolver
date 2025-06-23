using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Abstractions;

public interface IAIConnector
{
  public Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default);

  public Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default);
}