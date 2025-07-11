using AutoIssueResolver.Application.Abstractions;

namespace AutoIssueResolver.Application;

/// <summary>
/// Contains metadata about the current run.
/// </summary>
public class RunMetadata: IRunMetadata
{
  /// <inheritdoc />
  public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

  /// <inheritdoc />
  public string BranchName { get; set; } = string.Empty;

  /// <inheritdoc />
  public string? CacheName { get; set; } = string.Empty;
}
