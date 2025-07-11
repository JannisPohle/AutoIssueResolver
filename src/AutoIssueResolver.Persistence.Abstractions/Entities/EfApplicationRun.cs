namespace AutoIssueResolver.Persistence.Abstractions.Entities;

public class EfApplicationRun
{
  public required string Id { get; set; }

  public required string Branch { get; set; }

  public virtual List<EfRequest>? Requests { get; set; }

  public DateTime StartTimeUtc { get; set; }

  public DateTime? EndTimeUtc { get; set; }
}