using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Domain.Specifications;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class SpecificationConfiguration : IEntityTypeConfiguration<Specification>
{
    public void Configure(EntityTypeBuilder<Specification> builder)
    {
        var nullableTimestampConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
            value => value.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value)
                : null);

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

        builder.Property(specification => specification.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(SpecificationStatus.Draft)
            .IsRequired();

        builder.Property(specification => specification.ApprovedAtUtc)
            .HasConversion(nullableTimestampConverter);

        builder.Property(specification => specification.ImplementedAtUtc)
            .HasConversion(nullableTimestampConverter);

        builder.Property(specification => specification.VerifiedAtUtc)
            .HasConversion(nullableTimestampConverter);

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

        builder.Property(specification => specification.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(specification => specification.AcceptanceCriteriaVersion)
            .IsRequired();

        builder.Property(specification => specification.ImplementationTasksVersion)
            .IsRequired();
    }
}
