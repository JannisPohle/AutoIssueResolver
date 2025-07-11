using AutoIssueResolver.AIConnector.Abstractions.Models;

namespace AutoIssueResolver.AIConnector.Abstractions;

/// <summary>
///   Interface for connecting to an AI service.
/// </summary>
public interface IAIConnector
{
  #region Methods

  /// <summary>
  ///   Checks if the connector can handle the specified AI model.
  /// </summary>
  Task<bool> CanHandleModel(AIModels model, CancellationToken cancellationToken = default);

  /// <summary>
  ///   Gets a response from the AI for the given prompt.
  /// </summary>
  Task<Response> GetResponse(Prompt prompt, CancellationToken cancellationToken = default);

  /// <summary>
  ///   Sets up caching for the connector.
  /// </summary>
  Task SetupCaching(CancellationToken cancellationToken = default);

  #endregion
}