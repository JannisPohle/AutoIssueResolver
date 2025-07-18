namespace AutoIssueResolver.AIConnectors.Base;

public class UnsuccessfulResultException: Exception
{
  #region Properties

  /// <summary>
  /// Indicates whether the operation can be retried.
  /// </summary>
  public bool CanRetry { get; }

  #endregion

  #region Constructors

  public UnsuccessfulResultException(string? message, bool canRetry)
    : base(message)
  {
    CanRetry = canRetry;
  }

  public UnsuccessfulResultException(string? message, Exception? innerException, bool canRetry)
    : base(message, innerException)
  {
    CanRetry = canRetry;
  }

  #endregion
}