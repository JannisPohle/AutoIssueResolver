using AutoIssueResolver.Persistence.Sqlite.EntityConfiguration;
using Microsoft.EntityFrameworkCore;

namespace AutoIssueResolver.Persistence.Sqlite;

public class ReportingContextSqlite(DbContextOptions options): ReportingContext(options)
{
  protected override void ApplyModelConfiguration(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfiguration(new EfApplicationRunConfigurationSqlite());
    modelBuilder.ApplyConfiguration(new EfRequestConfigurationSqlite());
  }
}