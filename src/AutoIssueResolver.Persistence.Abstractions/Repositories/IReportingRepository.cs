using AutoIssueResolver.Persistence.Abstractions.Entities;

namespace AutoIssueResolver.Persistence.Abstractions.Repositories;

public interface IReportingRepository
{
  Task InitializeApplicationRun(CancellationToken token = default);

  Task EndApplicationRun(CancellationToken token = default);

  Task<EfRequest> InitializeRequest(EfRequestType requestType, string? codeSmellReference = null, CancellationToken token = default);

  Task IncrementRequestRetries(string requestId, CancellationToken token = default);

  Task EndRequest(string requestId, int totalTokensUsed, int? cachedTokens = null, int? promptTokens = null, int? responseTokens = null, CancellationToken token = default);
}