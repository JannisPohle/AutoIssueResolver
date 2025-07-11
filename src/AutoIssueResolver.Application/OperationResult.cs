namespace AutoIssueResolver.Application;

/// <summary>
/// Represents the result of an operation, including status and error information.
/// </summary>
public class OperationResult
{
  /// <summary>
  /// A successful result that allows continuation.
  /// </summary>
  public static OperationResult SuccessfulResult => new OperationResult { Success = true, CanContinue = true, };

  /// <summary>
  /// A fatal result that does not allow continuation.
  /// </summary>
  /// <param name="exception">The exception that caused the failure.</param>
  /// <param name="exitCode">The exit code for the failure.</param>
  public static OperationResult FatalResult(Exception? exception, ExitCodes exitCode) => new OperationResult { Success = false, CanContinue = false, ExitCodes = exitCode, Exception = exception, };

  /// <summary>
  /// A warning result that allows continuation.
  /// </summary>
  /// <param name="exception">The exception that caused the warning.</param>
  public static OperationResult WarningResult(Exception? exception) => new OperationResult { Success = false, CanContinue = true, Exception = exception, };

  /// <summary>
  /// Indicates if the operation was successful.
  /// </summary>
  public bool Success { get; set; } = false;

  /// <summary>
  /// Indicates if the process can continue after this operation.
  /// </summary>
  public bool CanContinue { get; set; } = true;

  /// <summary>
  /// Exception thrown during the operation, if <see cref="Success"/> indicates an error.
  /// </summary>
  public Exception? Exception { get; set; }

  /// <summary>
  /// Exit code(s) associated with the operation, if the process cannot continue based on <see cref="CanContinue"/>.
  /// </summary>
  public ExitCodes ExitCodes { get; set; }
}
