using AutoIssueResolver.Persistence.Abstractions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutoIssueResolver.Persistence.Sqlite;

public static class Extensions
{
  #region Methods

  public static IServiceCollection AddSqlitePersistence(this IServiceCollection services,
                                                        DatabaseConfiguration? configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    services.AddDbContext<ReportingContext, ReportingContextSqlite>(options =>
    {
      options.UseLazyLoadingProxies()
             .UseSqlite(configuration.ConnectionString);
    });

    services.AddRepositories();

    return services;
  }

  #endregion
}