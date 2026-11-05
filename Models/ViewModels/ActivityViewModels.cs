using Microsoft.AspNetCore.Mvc.Rendering;

namespace ACXIOMCRM.Models.ViewModels;

public class ActivityListViewModel
{
    public IReadOnlyList<ActivityRowViewModel> Activities { get; init; } = Array.Empty<ActivityRowViewModel>();
    public string? Search { get; init; }
    public ActivityType? Type { get; init; }
    public ActivityStatus? Status { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
}

public class ActivityRowViewModel
{
    public CrmActivity Activity { get; init; } = new();
    public ActivityStatus DisplayStatus { get; init; }
}

public class ActivityDetailsViewModel
{
    public CrmActivity Activity { get; init; } = new();
    public ActivityStatus DisplayStatus { get; init; }
}

public class ActivityFormOptions
{
    public IReadOnlyList<SelectListItem> Customers { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> Leads { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> Opportunities { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> Employees { get; init; } = Array.Empty<SelectListItem>();
}
