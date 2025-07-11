namespace AutoIssueResolver.Application.Abstractions;

/// <summary>
/// Contains metadata about the current run to fix code smells
/// </summary>
public interface IRunMetadata
{
  string CorrelationId { get; set; }

  /// <summary>
  /// Name of the branch used for adding the fixes, if available
  /// </summary>
  string? BranchName { get; set; }

  /// <summary>
  /// The name of the cache used for the AI agent, if available
  /// </summary>
  string? CacheName { get; set; }
}