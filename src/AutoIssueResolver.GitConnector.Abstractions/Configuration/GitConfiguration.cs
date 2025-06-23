using System.Security;

namespace AutoIssueResolver.GitConnector.Abstractions.Configuration;

public record GitConfiguration()
{
  public string Repository { get; set; } = string.Empty;

  public string Branch { get; set; } = string.Empty;

  public string CommitMessageTemplate { get; set; } = string.Empty;

  public GitCredentials Credentials { get; set; }
}

public record GitCredentials()
{
  public string Username { get; set; } = string.Empty;

  public string Password { get; set; } = string.Empty;
}
