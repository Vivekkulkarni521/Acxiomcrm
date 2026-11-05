using ACXIOMCRM.Models;

namespace ACXIOMCRM.Models.ViewModels;

public class LeadListViewModel
{
    public IReadOnlyList<Lead> Leads { get; init; } = Array.Empty<Lead>();
    public string? Search { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<string> StatusOptions { get; init; } = LeadStatuses.All;
}
