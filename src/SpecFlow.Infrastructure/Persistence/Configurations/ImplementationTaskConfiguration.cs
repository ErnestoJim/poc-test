using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Domain.Specifications;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class ImplementationTaskConfiguration
    : IEntityTypeConfiguration<ImplementationTask>
{
    public void Configure(EntityTypeBuilder<ImplementationTask> builder)
    {
        builder.ToTable(
            "ImplementationTasks",
            table => table.HasCheckConstraint(
                "CK_ImplementationTasks_Position_Positive",
                "\"Position\" > 0"));

        builder.HasKey(implementationTask => implementationTask.Id);

        builder.Property(implementationTask => implementationTask.SpecificationId)
            .IsRequired();

        builder.HasOne<Specification>()
            .WithMany()
            .HasForeignKey(implementationTask => implementationTask.SpecificationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(implementationTask => implementationTask.Title)
            .HasMaxLength(ImplementationTask.MaxTitleLength)
            .IsRequired();

        builder.Property(implementationTask => implementationTask.NormalizedTitle)
            .HasMaxLength(ImplementationTask.MaxTitleLength)
            .IsRequired();

        builder.Property(implementationTask => implementationTask.Description)
            .HasMaxLength(ImplementationTask.MaxDescriptionLength);

        builder.Property(implementationTask => implementationTask.Position)
            .IsRequired();

        builder.Property(implementationTask => implementationTask.CreatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(implementationTask => implementationTask.UpdatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(implementationTask => implementationTask.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(implementationTask => new
        {
            implementationTask.SpecificationId,
            implementationTask.NormalizedTitle
        })
            .IsUnique();

        builder.HasIndex(implementationTask => new
        {
            implementationTask.SpecificationId,
            implementationTask.Position
        })
            .IsUnique();
    }
}
