using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoIssueResolver.Persistence.Sqlite;

public class ReportingContextDesignTimeFactory: IDesignTimeDbContextFactory<ReportingContext>
{
  #region Methods

  public ReportingContext CreateDbContext(string[] args)
  {
    return new ReportingContextSqlite(new DbContextOptionsBuilder<ReportingContext>().UseSqlite(":memory:").Options);
  }

  #endregion
}