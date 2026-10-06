namespace SpecFlow.Domain.FeatureProposals;

public sealed class FeatureProposalTransitionException(FeatureProposalStatus currentStatus)
    : InvalidOperationException(
        $"A feature proposal in status '{currentStatus}' cannot be decided again.")
{
    public FeatureProposalStatus CurrentStatus { get; } = currentStatus;
}
