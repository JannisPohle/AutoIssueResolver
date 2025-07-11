using AutoIssueResolver.Application.Abstractions;

namespace AutoIssueResolver.Application;

public class RunMetadata: IRunMetadata
{
  public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

  public string BranchName { get; set; } = string.Empty;

  public string? CacheName { get; set; } = string.Empty;
}