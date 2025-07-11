using AutoIssueResolver.Application.Abstractions.Models;
using AutoIssueResolver.GitConnector.Abstractions;
using AutoIssueResolver.GitConnector.Abstractions.Configuration;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoIssueResolver.GitConnector;

/// <summary>
///   Implementation of <see cref="ISourceCodeConnector" />  to interact with Git repositories.
/// </summary>
public class GitConnector(IOptions<SourceCodeConfiguration> configuration, ILogger<GitConnector> logger): ISourceCodeConnector
{
  #region Properties

  private static string LocalPath => Path.Join(Path.GetTempPath(), "_git");

  #endregion

  #region Methods

  /// <inheritdoc />
  public async Task CloneRepository(CancellationToken cancellationToken = default)
  {
    var cloneOptions = new CloneOptions(new FetchOptions())
    {
      BranchName = configuration.Value.Branch,
      Checkout = true,
    };

    if (!string.IsNullOrWhiteSpace(configuration.Value.Credentials?.Username))
    {
      cloneOptions.FetchOptions.CredentialsProvider = (url, fromUrl, types) => new UsernamePasswordCredentials
      {
        Username = configuration.Value.Credentials.Username,
        Password = configuration.Value.Credentials.Password,
      };
    }

    Repository.Clone(configuration.Value.Repository, LocalPath, cloneOptions);

    using var repo = new Repository(LocalPath);
    repo.Config.Set("user.name", "\ud83e\udd16 AutoIssueResolver Bot");
    repo.Config.Set("user.email", "robot@git.com");
    repo.Config.Set("core.autocrlf", "true");
  }

  /// <inheritdoc />
  public async Task CheckoutBranch(CancellationToken cancellationToken = default)
  {
    using var repo = new Repository(LocalPath);
    var branch = repo.Branches[configuration.Value.Branch];

    if (branch == null)
    {
      throw new InvalidOperationException($"Branch {configuration.Value.Branch} does not exist.");
    }

    Commands.Checkout(repo, branch);
  }

  /// <inheritdoc />
  public async Task CreateBranch(string branchName, CancellationToken cancellationToken = default)
  {
    using var repo = new Repository(LocalPath);
    var branch = repo.Branches.Add(branchName, repo.Head.Tip);
    Commands.Checkout(repo, branch);
  }

  /// <inheritdoc />
  public async Task CommitChanges(string message, CancellationToken cancellationToken = default)
  {
    using var repo = new Repository(LocalPath);
    Commands.Stage(repo, "*");
    var author = repo.Config.BuildSignature(DateTimeOffset.Now);
    var committer = author;
    repo.Commit(message, author, committer);
  }

  /// <inheritdoc />
  public async Task PushChanges(CancellationToken cancellationToken = default)
  {
    try
    {
      using (var repo = new Repository(LocalPath))
      {
        var remote = repo.Network.Remotes["origin"];

        if (remote == null)
        {
          throw new InvalidOperationException("Remote 'origin' does not exist.");
        }

        var pushOptions = new PushOptions();

        if (!string.IsNullOrWhiteSpace(configuration.Value.Credentials?.Username))
        {
          pushOptions.CredentialsProvider = (url, fromUrl, types) => new UsernamePasswordCredentials
          {
            Username = configuration.Value.Credentials.Username,
            Password = configuration.Value.Credentials.Password,
          };
        }

        repo.Network.Push(remote, repo.Head.CanonicalName, pushOptions);
      }
    }
    catch (AccessViolationException e)
    {
      logger.LogWarning(e, "Error while pusing changes to the remote");
    }
  }

  /// <inheritdoc />
  public async Task<string> GetFileContent(string filePath, CancellationToken cancellationToken = default)
  {
    using var streamReader = new StreamReader(Path.Join(LocalPath, filePath));
    var fileContent = await streamReader.ReadToEndAsync(cancellationToken);

    return fileContent;
  }

  /// <inheritdoc />
  public async Task<List<SourceFile>> GetAllFiles(string extensionFilter = "*cs", CancellationToken cancellationToken = default)
  {
    var files = new List<SourceFile>();

    //TODO maybe exclude the test projects
    foreach (var filePath in Directory.EnumerateFiles(LocalPath, extensionFilter, SearchOption.AllDirectories))
    {
      var relativePath = Path.GetRelativePath(LocalPath, filePath);
      files.Add(new SourceFile
      {
        FilePath = relativePath,
        FileContent = await GetFileContent(relativePath, cancellationToken), //Remove local path from file path, since this will be added back in GetFileContent
      });
    }

    return files;
  }

  /// <inheritdoc />
  public async Task UpdateFileContent(string filePath, string content, CancellationToken cancellationToken = default)
  {
    var finalPath = Path.Join(LocalPath, filePath);

    if (!File.Exists(finalPath))
    {
      throw new InvalidOperationException($"File {filePath} does not exist.");
    }

    await using var writer = new StreamWriter(finalPath);
    await writer.WriteAsync(content);
  }

  #endregion
}