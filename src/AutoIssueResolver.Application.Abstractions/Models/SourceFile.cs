namespace AutoIssueResolver.Application.Abstractions.Models;

/// <summary>
/// Represents a source code file.
/// </summary>
public class SourceFile
{
  /// <summary>
  /// Path to the file.
  /// </summary>
  public string FilePath { get; set; }

  /// <summary>
  /// Content of the file.
  /// </summary>
  public string FileContent { get; set; }
}
