using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Domain.Specifications;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class SpecificationConfiguration : IEntityTypeConfiguration<Specification>
{
    public void Configure(EntityTypeBuilder<Specification> builder)
    {
        builder.ToTable("Specifications");

        builder.HasKey(specification => specification.Id);

        builder.Property(specification => specification.FeatureProposalId)
            .IsRequired();

        builder.HasOne<FeatureProposal>()
            .WithOne()
            .HasForeignKey<Specification>(specification => specification.FeatureProposalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(specification => specification.FeatureProposalId)
            .IsUnique();

        builder.Property(specification => specification.Content)
            .HasMaxLength(Specification.MaxContentLength)
            .IsRequired();

        builder.Property(specification => specification.CreatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(specification => specification.UpdatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(specification => specification.AcceptanceCriteriaVersion)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(specification => specification.ImplementationTasksVersion)
            .IsRequired();
    }
}
