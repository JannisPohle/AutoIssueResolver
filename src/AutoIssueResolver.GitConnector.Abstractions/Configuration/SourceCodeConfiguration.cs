using System.Security;

namespace AutoIssueResolver.GitConnector.Abstractions.Configuration;

/// <summary>
/// Configuration for connecting to a source code repository.
/// </summary>
public record SourceCodeConfiguration()
{
  /// <summary>
  /// Repository URL.
  /// </summary>
  public string Repository { get; set; } = string.Empty;

  /// <summary>
  /// Branch name.
  /// </summary>
  public string Branch { get; set; } = string.Empty;

  /// <summary>
  /// Template for commit messages.
  /// </summary>
  public string CommitMessageTemplate { get; set; } = string.Empty;

  /// <summary>
  /// Credentials for repository access.
  /// </summary>
  public SourceCodeCredentials Credentials { get; set; }
}

/// <summary>
/// Credentials for accessing a source code repository.
/// </summary>
public record SourceCodeCredentials()
{
  /// <summary>
  /// Username for authentication.
  /// </summary>
  public string Username { get; set; } = string.Empty;

  /// <summary>
  /// Password for authentication.
  /// </summary>
  public string Password { get; set; } = string.Empty;
}
