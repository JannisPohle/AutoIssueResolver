using AutoIssueResolver.Persistence.Sqlite.EntityConfiguration;
using Microsoft.EntityFrameworkCore;

namespace AutoIssueResolver.Persistence.Sqlite;

/// <summary>
///   SQLite-specific ReportingContext.
/// </summary>
public class ReportingContextSqlite(DbContextOptions options): ReportingContext(options)
{
  #region Methods

  /// <summary>
  ///   Applies SQLite-specific model configurations.
  /// </summary>
  protected override void ApplyModelConfiguration(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfiguration(new EfApplicationRunConfigurationSqlite());
    modelBuilder.ApplyConfiguration(new EfRequestConfigurationSqlite());
  }

  #endregion
}