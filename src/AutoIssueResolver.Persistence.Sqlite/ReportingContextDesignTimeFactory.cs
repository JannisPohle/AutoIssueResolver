using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoIssueResolver.Persistence.Sqlite;

/// <summary>
/// Design-time factory for creating a ReportingContext instance.
/// </summary>
public class ReportingContextDesignTimeFactory: IDesignTimeDbContextFactory<ReportingContext>
{
  #region Methods

  /// <summary>
  /// Creates a new ReportingContext instance for design-time operations.
  /// </summary>
  public ReportingContext CreateDbContext(string[] args)
  {
    return new ReportingContextSqlite(new DbContextOptionsBuilder<ReportingContext>().UseSqlite(":memory:").Options);
  }

  #endregion
}
