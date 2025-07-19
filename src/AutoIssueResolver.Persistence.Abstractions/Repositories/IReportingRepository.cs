using AutoIssueResolver.Persistence.Abstractions.Entities;

namespace AutoIssueResolver.Persistence.Abstractions.Repositories;

/// <summary>
///   Repository for reporting and tracking application runs and requests.
/// </summary>
public interface IReportingRepository
{
  #region Methods

  /// <summary>
  ///   Initializes a new application run.
  /// </summary>
  Task InitializeApplicationRun(CancellationToken token = default);

  /// <summary>
  ///   Marks the end of the current application run.
  /// </summary>
  Task EndApplicationRun(CancellationToken token = default);

  /// <summary>
  ///   Initializes a new request.
  /// </summary>
  Task<EfRequest> InitializeRequest(EfRequestType requestType, string? codeSmellReference = null, CancellationToken token = default);

  /// <summary>
  ///   Increments the retry count for a request.
  /// </summary>
  Task IncrementRequestRetries(string requestId, int totalTokensUsed = 0, int cachedTokens = 0, int promptTokens = 0, int responseTokens = 0, CancellationToken token = default);

  /// <summary>
  ///   Marks the end of a request and records token usage.
  /// </summary>
  Task EndRequest(string requestId, EfRequestStatus finalState, int totalTokensUsed = 0, int cachedTokens = 0, int promptTokens = 0, int responseTokens = 0, CancellationToken token = default);

  #endregion
}