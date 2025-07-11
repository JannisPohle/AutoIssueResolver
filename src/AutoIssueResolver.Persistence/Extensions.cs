using AutoIssueResolver.Persistence.Abstractions.Repositories;
using AutoIssueResolver.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AutoIssueResolver.Persistence;

public static class Extensions
{
  #region Methods

  public static IServiceCollection AddRepositories(this IServiceCollection services)
  {
    services.AddScoped<IReportingRepository, ReportingRepository>();

    return services;
  }

  #endregion
}