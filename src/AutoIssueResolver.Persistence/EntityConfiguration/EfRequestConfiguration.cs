using AutoIssueResolver.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoIssueResolver.Persistence.EntityConfiguration;

public abstract class EfRequestConfiguration: IEntityTypeConfiguration<EfRequest>
{
  #region Methods

  public void Configure(EntityTypeBuilder<EfRequest> builder)
  {
    builder.HasKey(e => e.Id);

    builder.Property(e => e.Id)
           .HasMaxLength(64)
           .IsRequired();
  }

  #endregion
}