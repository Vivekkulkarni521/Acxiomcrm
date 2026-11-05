namespace ACXIOMCRM.Models.ViewModels;

public class FollowUpListViewModel
{
    public IReadOnlyList<FollowUp> UpcomingPlanned { get; init; } = Array.Empty<FollowUp>();
    public IReadOnlyList<FollowUp> OverduePlanned { get; init; } = Array.Empty<FollowUp>();
    public IReadOnlyList<FollowUp> OtherFollowUps { get; init; } = Array.Empty<FollowUp>();
    public string? Search { get; init; }
    public FollowUpStatus? Status { get; init; }
    public IReadOnlyList<FollowUpStatus> StatusOptions { get; init; } = Enum.GetValues<FollowUpStatus>();
}
