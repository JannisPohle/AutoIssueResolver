namespace AutoIssueResolver.Persistence.Abstractions.Entities;

public class EfRequest
{
  public string Id { get; set; }

  public EfRequestType RequestType { get; set; }

  public int TotalTokensUsed { get; set; }

  public int? CachedTokens { get; set; }

  public int? PromptTokens { get; set; }

  public int? ResponseTokens { get; set; }

  public DateTime StartTimeUtc { get; set; }

  public DateTime? EndTimeUtc { get; set; }

  public int Retries { get; set; }

  public string? CodeSmellReference { get; set; }

  public virtual EfApplicationRun ApplicationRun { get; set; }
}