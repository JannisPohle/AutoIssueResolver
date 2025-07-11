using AutoIssueResolver.Persistence.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoIssueResolver.Persistence.EntityConfiguration;

public abstract class EfApplicationRunConfiguration: IEntityTypeConfiguration<EfApplicationRun>
{
  #region Methods

  public void Configure(EntityTypeBuilder<EfApplicationRun> builder)
  {
    builder.HasKey(x => x.Id);

    builder.Property(x => x.Id)
           .HasMaxLength(64)
           .IsRequired();

    builder.HasMany(x => x.Requests)
           .WithOne(x => x.ApplicationRun)
           .OnDelete(DeleteBehavior.Cascade);
  }

  #endregion
}