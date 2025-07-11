using AutoIssueResolver.Persistence.Abstractions.Repositories;
using AutoIssueResolver.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AutoIssueResolver.Persistence;

/// <summary>
///   Extension methods for registering persistence services.
/// </summary>
public static class Extensions
{
  #region Methods

  /// <summary>
  ///   Registers repository services for dependency injection.
  /// </summary>
  public static IServiceCollection AddRepositories(this IServiceCollection services)
  {
    services.AddScoped<IReportingRepository, ReportingRepository>();

    return services;
  }

  #endregion
}