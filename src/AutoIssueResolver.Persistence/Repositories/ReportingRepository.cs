using AutoIssueResolver.Application.Abstractions;
using AutoIssueResolver.Persistence.Abstractions.Entities;
using AutoIssueResolver.Persistence.Abstractions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AutoIssueResolver.Persistence.Repositories;

/// <summary>
///   Implements the data access layer for interacting with reporting data.
/// </summary>
public class ReportingRepository(ReportingContext reportingContext, IRunMetadata metadata): IReportingRepository
{
  #region Methods

  /// <inheritdoc />
  public async Task InitializeApplicationRun(CancellationToken token = default)
  {
    var applicationRun = new EfApplicationRun
    {
      Id = metadata.CorrelationId,
      Branch = metadata.BranchName ?? string.Empty,
      StartTimeUtc = DateTime.UtcNow,
    };

    await reportingContext.ApplicationRuns.AddAsync(applicationRun, token);
    await reportingContext.SaveChangesAsync(token);
  }

  /// <inheritdoc />
  public async Task EndApplicationRun(CancellationToken token = default)
  {
    var applicationRun = await FindCurrentApplicationRun(token);

    applicationRun.EndTimeUtc = DateTime.UtcNow;
    await reportingContext.SaveChangesAsync(token);
  }

  /// <inheritdoc />
  public async Task<EfRequest> InitializeRequest(EfRequestType requestType, string? codeSmellReference = null, CancellationToken token = default)
  {
    var applicationRun = await FindCurrentApplicationRun(token);

    var request = new EfRequest
    {
      Id = Guid.NewGuid().ToString(), // Generate a unique ID for the request
      RequestType = requestType,
      StartTimeUtc = DateTime.UtcNow,
      ApplicationRun = applicationRun,
      Retries = 0, // Initialize retries to 0
      CodeSmellReference = codeSmellReference,
    };

    applicationRun.Requests ??= [];
    applicationRun.Requests.Add(request);

    await reportingContext.SaveChangesAsync(token);

    return request;
  }

  /// <inheritdoc />
  public async Task IncrementRequestRetries(string requestId, CancellationToken token = default)
  {
    var request = await FindRequest(requestId, token);

    request.Retries++;
    await reportingContext.SaveChangesAsync(token);
  }

  /// <inheritdoc />
  public async Task EndRequest(string requestId, int totalTokensUsed, int? cachedTokens = null, int? promptTokens = null, int? responseTokens = null, CancellationToken token = default)
  {
    var request = await FindRequest(requestId, token);

    request.EndTimeUtc = DateTime.UtcNow;
    request.TotalTokensUsed = totalTokensUsed;
    request.CachedTokens = cachedTokens;
    request.PromptTokens = promptTokens;
    request.ResponseTokens = responseTokens;

    await reportingContext.SaveChangesAsync(token);
  }

  private async Task<EfApplicationRun> FindCurrentApplicationRun(CancellationToken token)
  {
    var applicationRun = await reportingContext.ApplicationRuns.FirstOrDefaultAsync(ar => ar.Id == metadata.CorrelationId, token);

    if (applicationRun == null)
    {
      throw new InvalidOperationException($"Application run with ID {metadata.CorrelationId} not found.");
    }

    return applicationRun;
  }

  private async Task<EfRequest> FindRequest(string requestId, CancellationToken token)
  {
    var request = await reportingContext.Requests.FirstOrDefaultAsync(r => r.Id == requestId, token);

    if (request == null)
    {
      throw new InvalidOperationException($"Request with ID {requestId} not found.");
    }

    return request;
  }

  #endregion
}