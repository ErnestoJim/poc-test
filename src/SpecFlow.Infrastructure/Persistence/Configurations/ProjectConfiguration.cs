using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpecFlow.Domain.Projects;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Name)
            .HasMaxLength(Project.MaxNameLength)
            .IsRequired();

        builder.Property(project => project.NormalizedName)
            .HasMaxLength(Project.MaxNameLength)
            .IsRequired();

        builder.HasIndex(project => project.NormalizedName)
            .IsUnique();

        builder.Property(project => project.Description)
            .HasMaxLength(Project.MaxDescriptionLength);

        builder.Property(project => project.CreatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();
    }
}
