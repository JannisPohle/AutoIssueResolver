namespace AutoIssueResolver.Persistence.Abstractions.Entities;

/// <summary>
///   Entity representing a request made during an application run.
/// </summary>
public class EfRequest
{
  #region Properties

  /// <summary>
  ///   Unique identifier for the request.
  /// </summary>
  public string Id { get; set; }

  /// <summary>
  ///   Type of the request.
  /// </summary>
  public EfRequestType RequestType { get; set; }

  /// <summary>
  ///   Total tokens used for this request.
  /// </summary>
  public int TotalTokensUsed { get; set; }

  /// <summary>
  ///   Number of cached tokens, if applicable.
  /// </summary>
  public int? CachedTokens { get; set; }

  /// <summary>
  ///   Number of prompt tokens, if applicable.
  /// </summary>
  public int? PromptTokens { get; set; }

  /// <summary>
  ///   Number of response tokens, if applicable.
  /// </summary>
  public int? ResponseTokens { get; set; }

  /// <summary>
  ///   Start time in UTC.
  /// </summary>
  public DateTime StartTimeUtc { get; set; }

  /// <summary>
  ///   End time in UTC, if available.
  /// </summary>
  public DateTime? EndTimeUtc { get; set; }

  /// <summary>
  ///   Number of retries for this request.
  /// </summary>
  public int Retries { get; set; }

  /// <summary>
  ///   Reference to the code smell, if applicable.
  /// </summary>
  public string? CodeSmellReference { get; set; }

  /// <summary>
  ///   Associated application run.
  /// </summary>
  public virtual EfApplicationRun ApplicationRun { get; set; }

  #endregion
}