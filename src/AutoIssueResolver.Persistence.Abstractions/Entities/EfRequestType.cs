namespace AutoIssueResolver.Persistence.Abstractions.Entities;

/// <summary>
///   Type of request made during an application run.
/// </summary>
public enum EfRequestType
{
  CacheCreation,
  FileUpload,
  CodeGeneration,
}