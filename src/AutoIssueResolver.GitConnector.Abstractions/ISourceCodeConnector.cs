using AutoIssueResolver.Application.Abstractions.Models;

namespace AutoIssueResolver.GitConnector.Abstractions;

/// <summary>
///   Interface for interacting with a source code repository.
/// </summary>
public interface ISourceCodeConnector
{
  #region Methods

  //TODO maybe combine some methods into a single, more general method (e.g. combine CloneRepository and CheckoutBranch into something like InitializeCodeRepository)

  /// <summary>
  ///   Clones the repository.
  /// </summary>
  Task CloneRepository(CancellationToken cancellationToken = default);

  /// <summary>
  ///   Checks out the specified branch.
  /// </summary>
  Task CheckoutBranch(CancellationToken cancellationToken = default);

  /// <summary>
  ///   Creates a new branch.
  /// </summary>
  Task CreateBranch(string branchName, CancellationToken cancellationToken = default);

  /// <summary>
  ///   Commits changes with the given message.
  /// </summary>
  Task CommitChanges(string message, CancellationToken cancellationToken = default);

  /// <summary>
  ///   Pushes committed changes to the remote repository.
  /// </summary>
  Task PushChanges(CancellationToken cancellationToken = default);

  /// <summary>
  ///   Gets the content of a file.
  /// </summary>
  Task<string> GetFileContent(string filePath, CancellationToken cancellationToken = default);

  /// <summary>
  ///   Updates the content of a file.
  /// </summary>
  Task UpdateFileContent(string filePath, string content, CancellationToken cancellationToken = default);

  /// <summary>
  ///   Gets all files matching the extension filter.
  /// </summary>
  Task<List<SourceFile>> GetAllFiles(string extensionFilter = "*cs", string? folderFilter = null, CancellationToken cancellationToken = default);

  #endregion
}