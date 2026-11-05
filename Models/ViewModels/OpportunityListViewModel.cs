namespace ACXIOMCRM.Models.ViewModels;

public class OpportunityListViewModel
{
    public IReadOnlyList<Opportunity> Opportunities { get; init; } = Array.Empty<Opportunity>();
    public IReadOnlyList<OpportunityStageSummary> StageSummaries { get; init; } = Array.Empty<OpportunityStageSummary>();
    public string? Search { get; init; }
    public OpportunityStage? Stage { get; init; }
    public decimal TotalPipelineValue { get; init; }
    public decimal WonRevenue { get; init; }
    public decimal OpenOpportunityValue { get; init; }
}

public class OpportunityStageSummary
{
    public OpportunityStage Stage { get; init; }
    public int Count { get; init; }
    public decimal Amount { get; init; }
}
