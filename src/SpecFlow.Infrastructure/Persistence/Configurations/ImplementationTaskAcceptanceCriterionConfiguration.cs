using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpecFlow.Domain.AcceptanceCriteria;
using SpecFlow.Domain.ImplementationTasks;
using SpecFlow.Domain.Traceability;

namespace SpecFlow.Infrastructure.Persistence.Configurations;

internal sealed class ImplementationTaskAcceptanceCriterionConfiguration
    : IEntityTypeConfiguration<ImplementationTaskAcceptanceCriterion>
{
    public void Configure(EntityTypeBuilder<ImplementationTaskAcceptanceCriterion> builder)
    {
        builder.ToTable("ImplementationTaskAcceptanceCriteria");

        builder.HasKey(link => new
        {
            link.ImplementationTaskId,
            link.AcceptanceCriterionId
        });

        builder.HasOne<ImplementationTask>()
            .WithMany()
            .HasForeignKey(link => link.ImplementationTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AcceptanceCriterion>()
            .WithMany()
            .HasForeignKey(link => link.AcceptanceCriterionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(link => new
        {
            link.AcceptanceCriterionId,
            link.ImplementationTaskId
        });
    }
}
