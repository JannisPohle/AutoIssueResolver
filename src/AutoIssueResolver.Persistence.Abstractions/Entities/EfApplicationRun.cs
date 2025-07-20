namespace AutoIssueResolver.Persistence.Abstractions.Entities;

/// <summary>
///   Entity representing an application run.
/// </summary>
public class EfApplicationRun
{
  #region Properties

  /// <summary>
  ///   Unique identifier for the run.
  /// </summary>
  public required string Id { get; set; }

  /// <summary>
  ///   Branch associated with the run.
  /// </summary>
  public required string Branch { get; set; }

  /// <summary>
  /// Model used for the run
  /// </summary>
  public required string Model { get; set; }

  /// <summary>
  ///   Requests associated with the run.
  /// </summary>
  public virtual List<EfRequest>? Requests { get; set; }

  /// <summary>
  ///   Start time in UTC.
  /// </summary>
  public DateTime StartTimeUtc { get; set; }

  /// <summary>
  ///   End time in UTC, if available.
  /// </summary>
  public DateTime? EndTimeUtc { get; set; }

  #endregion
}