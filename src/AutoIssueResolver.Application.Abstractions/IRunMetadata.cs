namespace AutoIssueResolver.Application.Abstractions;

/// <summary>
///   Contains metadata about the current run to fix code smells
/// </summary>
public interface IRunMetadata
{
  #region Properties

  /// <summary>
  ///   Correlation ID for the run.
  /// </summary>
  string CorrelationId { get; set; }

  /// <summary>
  ///   Name of the branch used for adding the fixes, if available
  /// </summary>
  string? BranchName { get; set; }

  #endregion
}