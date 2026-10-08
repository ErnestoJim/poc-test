using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpecFlow.Domain.Projects;
using SpecFlow.Domain.TechnicalDecisions;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class TechnicalDecisionConfiguration
    : IEntityTypeConfiguration<TechnicalDecision>
{
    public void Configure(EntityTypeBuilder<TechnicalDecision> builder)
    {
        builder.ToTable("TechnicalDecisions");

        builder.HasKey(decision => decision.Id);

        builder.Property(decision => decision.ProjectId)
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(decision => decision.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(decision => decision.Title)
            .HasMaxLength(TechnicalDecision.MaxTitleLength)
            .IsRequired();

        builder.Property(decision => decision.Content)
            .HasMaxLength(TechnicalDecision.MaxContentLength)
            .IsRequired();

        builder.Property(decision => decision.CreatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(decision => decision.UpdatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(decision => decision.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(decision => new
        {
            decision.ProjectId,
            decision.CreatedAtUtc,
            decision.Id
        });
    }
}
