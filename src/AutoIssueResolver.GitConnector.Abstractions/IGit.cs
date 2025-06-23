namespace AutoIssueResolver.GitConnector.Abstractions;

public interface IGit
{
  Task CloneRepository(CancellationToken cancellationToken = default);
  Task CheckoutBranch(CancellationToken cancellationToken = default);
  Task CreateBranch(string branchName, CancellationToken cancellationToken = default);
  Task CommitChanges(string message, CancellationToken cancellationToken = default);
  Task PushChanges(CancellationToken cancellationToken = default);
  Task<string> GetFileContent(string filePath, CancellationToken cancellationToken = default);
  Task UpdateFileContent(string filePath, string content, CancellationToken cancellationToken = default);
}