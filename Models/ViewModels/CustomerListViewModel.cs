using ACXIOMCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ACXIOMCRM.Models.ViewModels;

public class CustomerListViewModel
{
    public IReadOnlyList<Customer> Customers { get; init; } = Array.Empty<Customer>();
    public string? Search { get; init; }
    public string? Company { get; init; }
    public string? AssignedToUserId { get; init; }
    public IReadOnlyList<string> Companies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SelectListItem> Assignees { get; init; } = Array.Empty<SelectListItem>();
}
