using AutoIssueResolver.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoIssueResolver.Persistence.EntityConfiguration;

/// <summary>
/// Base configuration for EfRequest entity.
/// </summary>
public abstract class EfRequestConfiguration: IEntityTypeConfiguration<EfRequest>
{
  #region Methods

  /// <summary>
  /// Configures the EfRequest entity.
  /// </summary>
  public void Configure(EntityTypeBuilder<EfRequest> builder)
  {
    builder.HasKey(e => e.Id);

    builder.Property(e => e.Id)
           .HasMaxLength(64)
           .IsRequired();
  }

  #endregion
}
