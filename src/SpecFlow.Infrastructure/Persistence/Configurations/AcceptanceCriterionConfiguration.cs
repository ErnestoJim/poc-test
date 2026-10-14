using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.Specifications;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class AcceptanceCriterionConfiguration
    : IEntityTypeConfiguration<AcceptanceCriterion>
{
    public void Configure(EntityTypeBuilder<AcceptanceCriterion> builder)
    {
        var nullableTimestampConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
            value => value.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value)
                : null);

        builder.ToTable(
            "AcceptanceCriteria",
            table => table.HasCheckConstraint(
                "CK_AcceptanceCriteria_Position_Positive",
                "\"Position\" > 0"));

        builder.HasKey(criterion => criterion.Id);

        builder.Property(criterion => criterion.SpecificationId)
            .IsRequired();

        builder.HasOne<Specification>()
            .WithMany()
            .HasForeignKey(criterion => criterion.SpecificationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(criterion => criterion.Content)
            .HasMaxLength(AcceptanceCriterion.MaxContentLength)
            .IsRequired();

        builder.Property(criterion => criterion.ContentHash)
            .IsFixedLength()
            .HasMaxLength(AcceptanceCriterion.ContentHashLength)
            .IsRequired();

        builder.Property(criterion => criterion.Position)
            .IsRequired();

        builder.Property(criterion => criterion.CreatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(criterion => criterion.UpdatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(criterion => criterion.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(criterion => criterion.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasDefaultValue(AcceptanceCriterionVerificationStatus.Unverified)
            .IsRequired();

        builder.Property(criterion => criterion.VerificationEvidence)
            .HasMaxLength(AcceptanceCriterion.MaxVerificationEvidenceLength);

        builder.Property(criterion => criterion.CheckedAtUtc)
            .HasConversion(nullableTimestampConverter);

        builder.Property(criterion => criterion.VerificationVersion)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(criterion => new
        {
            criterion.SpecificationId,
            criterion.ContentHash
        })
            .IsUnique();

        builder.HasIndex(criterion => new
        {
            criterion.SpecificationId,
            criterion.Position
        })
            .IsUnique();
    }
}
