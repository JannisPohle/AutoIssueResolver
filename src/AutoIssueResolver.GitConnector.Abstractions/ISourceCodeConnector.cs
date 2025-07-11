using AutoIssueResolver.Application.Abstractions.Models;

namespace AutoIssueResolver.GitConnector.Abstractions;

public interface ISourceCodeConnector
{
  //TODO maybe combine some methods into a single, more general method (e.g. combine CloneRepository and CheckoutBranch into something like InitializeCodeRepository)
  Task CloneRepository(CancellationToken cancellationToken = default);
  Task CheckoutBranch(CancellationToken cancellationToken = default);
  Task CreateBranch(string branchName, CancellationToken cancellationToken = default);
  Task CommitChanges(string message, CancellationToken cancellationToken = default);
  Task PushChanges(CancellationToken cancellationToken = default);
  Task<string> GetFileContent(string filePath, CancellationToken cancellationToken = default);
  Task UpdateFileContent(string filePath, string content, CancellationToken cancellationToken = default);

  Task<List<SourceFile>> GetAllFiles(string extensionFilter = "*cs", CancellationToken cancellationToken = default);
}