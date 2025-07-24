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
    logger.LogInformation("Cloning repository {Repository} (branch: {Branch}) to {LocalPath}", configuration.Value.Repository, configuration.Value.Branch, LocalPath);

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

    logger.LogInformation("Repository cloned and configured at {LocalPath}", LocalPath);
  }

  /// <inheritdoc />
  public async Task CreateBranch(string branchName, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Creating and checking out new branch {BranchName}", branchName);
    using var repo = new Repository(LocalPath);
    var branch = repo.Branches.Add(branchName, repo.Head.Tip);
    Commands.Checkout(repo, branch);
    logger.LogInformation("Created and checked out branch {BranchName}", branchName);
  }

  /// <inheritdoc />
  public async Task CommitChanges(string message, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Committing changes with message: {Message}", message);
    using var repo = new Repository(LocalPath);
    Commands.Stage(repo, "*");
    var author = repo.Config.BuildSignature(DateTimeOffset.Now);
    var committer = author;
    repo.Commit(message, author, committer);
    logger.LogInformation("Changes committed.");
  }

  /// <inheritdoc />
  public async Task PushChanges(CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Pushing changes to remote repository...");
    try
    {
      using (var repo = new Repository(LocalPath))
      {
        var remote = repo.Network.Remotes["origin"];

        if (remote == null)
        {
          logger.LogError("Remote 'origin' does not exist.");
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
      logger.LogInformation("Changes pushed to remote repository.");
    }
    catch (AccessViolationException e)
    {
      logger.LogWarning(e, "Error while pushing changes to the remote");
    }
  }

  /// <inheritdoc />
  public async Task<string> GetFileContent(string filePath, CancellationToken cancellationToken = default)
  {
    logger.LogDebug("Getting file content for {FilePath}", filePath);
    using var streamReader = new StreamReader(Path.Join(LocalPath, filePath));
    var fileContent = await streamReader.ReadToEndAsync(cancellationToken);

    logger.LogTrace("Read {Length} characters from {FilePath}", fileContent.Length, filePath);
    return fileContent;
  }

  /// <inheritdoc />
  public async Task<List<SourceFile>> GetAllFiles(string extensionFilter = "*cs", string? folderFilter = null, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Getting all files with extension filter {ExtensionFilter} and folder filter {FolderFilter}", extensionFilter, folderFilter);
    var files = new List<SourceFile>();

    //TODO maybe make exclusions configurable via the configuration
    foreach (var filePath in Directory.EnumerateFiles(LocalPath, extensionFilter, SearchOption.AllDirectories).OrderBy(file => file, StringComparer.InvariantCultureIgnoreCase))
    {
      if (filePath.Contains("bin") || filePath.Contains("obj"))
      {
        logger.LogDebug("Skipping file {FilePath} as it is in bin or obj directory", filePath);
        continue; // Skip files in bin or obj directories
      }

      var relativePath = Path.GetRelativePath(LocalPath, filePath);

      if (relativePath.StartsWith('.') || relativePath.Contains("IntegrationTests") || relativePath.Contains("UnitTests"))
      {
        logger.LogDebug("Skipping file {FilePath} as it is a test file or in a hidden directory", relativePath);
        continue;
      }

      if (!string.IsNullOrWhiteSpace(folderFilter) && !relativePath.Contains(folderFilter, StringComparison.InvariantCultureIgnoreCase) && !relativePath.Contains("Program.cs", StringComparison.InvariantCultureIgnoreCase))
      {
        logger.LogDebug("Skipping file {FilePath} as it does not contain the folder filter {FolderFilter}", relativePath, folderFilter);
        continue; // Skip files that do not match the folder filter, but exclude Program.cs, because it is the entry point of the application
      }

      files.Add(new SourceFile
      {
        FilePath = relativePath,
        FileContent = await GetFileContent(relativePath, cancellationToken),
      });
    }

    logger.LogInformation("Found {FileCount} files with extension filter {ExtensionFilter} and folder filter {FolderFilter}", files.Count, extensionFilter, folderFilter);
    return files;
  }

  /// <inheritdoc />
  public async Task UpdateFileContent(string filePath, string content, CancellationToken cancellationToken = default)
  {
    logger.LogInformation("Updating file content for {FilePath}", filePath);
    var finalPath = Path.Join(LocalPath, filePath);

    if (!File.Exists(finalPath))
    {
      logger.LogError("File {FilePath} does not exist.", filePath);
      throw new FileNotFoundException($"File {filePath} does not exist.");
    }

    await using var writer = new StreamWriter(finalPath);
    await writer.WriteAsync(content);
    logger.LogInformation("File {FilePath} updated.", filePath);
  }

  #endregion
}
