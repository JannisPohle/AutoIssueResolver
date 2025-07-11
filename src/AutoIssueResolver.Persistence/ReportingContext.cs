using AutoIssueResolver.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoIssueResolver.Persistence;

/// <summary>
///   Base DbContext for reporting application runs and requests.
/// </summary>
public abstract class ReportingContext: DbContext
{
  #region Properties

  /// <summary>
  ///   Application runs tracked in the context.
  /// </summary>
  public DbSet<EfApplicationRun> ApplicationRuns { get; set; }

  /// <summary>
  ///   Requests tracked in the context.
  /// </summary>
  public DbSet<EfRequest> Requests { get; set; }

  #endregion

  #region Constructors

  /// <summary>
  ///   Initializes a new instance of the <see cref="ReportingContext" /> class.
  /// </summary>
  /// <param name="options">The options for this context.</param>
  /// <remarks>
  ///   See <see href="https://aka.ms/efcore-docs-dbcontext">DbContext lifetime, configuration, and initialization</see> and
  ///   <see href="https://aka.ms/efcore-docs-dbcontext-options">Using DbContextOptions</see> for more information.
  /// </remarks>
  protected ReportingContext(DbContextOptions options)
    : base(options)
  { }

  #endregion

  #region Methods

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    ApplyModelConfiguration(modelBuilder);
    base.OnModelCreating(modelBuilder);
  }

  /// <summary>
  ///   Apply database specific configuration for the models.
  /// </summary>
  /// <param name="modelBuilder"></param>
  protected abstract void ApplyModelConfiguration(ModelBuilder modelBuilder);

  #endregion
}