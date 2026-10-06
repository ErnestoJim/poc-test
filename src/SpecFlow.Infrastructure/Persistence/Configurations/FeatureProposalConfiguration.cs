using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SpecFlow.Domain.FeatureProposals;
using SpecFlow.Domain.Projects;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class FeatureProposalConfiguration : IEntityTypeConfiguration<FeatureProposal>
{
    public void Configure(EntityTypeBuilder<FeatureProposal> builder)
    {
        var nullableTimestampConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.ToUnixTimeMilliseconds() : null,
            value => value.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value)
                : null);

        builder.ToTable("FeatureProposals");

        builder.HasKey(proposal => proposal.Id);

        builder.Property(proposal => proposal.ProjectId)
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(proposal => proposal.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(proposal => proposal.ProjectId);

        builder.Property(proposal => proposal.Title)
            .HasMaxLength(FeatureProposal.MaxTitleLength)
            .IsRequired();

        builder.Property(proposal => proposal.Description)
            .HasMaxLength(FeatureProposal.MaxDescriptionLength);

        builder.Property(proposal => proposal.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(FeatureProposalStatus.Pending)
            .IsRequired();

        builder.Property(proposal => proposal.DecidedAtUtc)
            .HasConversion(nullableTimestampConverter);

        builder.Property(proposal => proposal.RejectionReason)
            .HasMaxLength(FeatureProposal.MaxRejectionReasonLength);

        builder.Property(proposal => proposal.CreatedAtUtc)
            .HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
            .IsRequired();

        builder.Property(proposal => proposal.Version)
            .IsConcurrencyToken()
            .IsRequired();
    }
}
