using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoIssueResolver.Persistence;

/// <summary>
///   Hosted service that runs database migrations on application startup.
/// </summary>
/// <remarks>
///   Implement as hosted service, instead of BackgroundService, to ensure that the migrations are finished, before other
///   services are started.
/// </remarks>
/// <param name="serviceScopeFactory">Factoring for creating the db context</param>
public class MigrationRunner(IServiceScopeFactory serviceScopeFactory, ILogger<MigrationRunner> logger): IHostedService
{
  #region Methods

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    //TODO figure out, how to automatically generate the sqlite file, if it does not exist yet
    try
    {
      await using var scope = serviceScopeFactory.CreateAsyncScope();
      var context = scope.ServiceProvider.GetRequiredService<ReportingContext>();
      logger.LogInformation("Starting Migration for the ReportingContext");
      await context.Database.MigrateAsync(cancellationToken);
      logger.LogInformation("Migration for the ReportingContext completed successfully");
    }
    catch (Exception e)
    {
      logger.LogError(e, "An error occurred while migrating the ReportingContext");

      throw;
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    // No-op for now, but can be used for cleanup if needed in the future.
  }

  #endregion
}