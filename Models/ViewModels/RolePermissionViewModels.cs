using System.ComponentModel.DataAnnotations;

namespace ACXIOMCRM.Models.ViewModels;

public sealed class RolePermissionEditViewModel
{
    public string? RoleId { get; set; }

    [Required, StringLength(64, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9 _-]*$", ErrorMessage = "Use letters, numbers, spaces, hyphens, or underscores.")]
    public string Name { get; set; } = string.Empty;

    public List<RolePermissionSelection> Permissions { get; set; } = new();
}

public sealed class RolePermissionSelection
{
    public string Module { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public bool Granted { get; set; }
}

public sealed class RoleManagementListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public bool IsBuiltIn { get; set; }
}

public sealed class ReportsViewModel
{
    public string Type { get; set; } = "customers";
    public string Title { get; set; } = string.Empty;
    public string? Search { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public IReadOnlyList<ReportOption> ReportOptions { get; set; } = Array.Empty<ReportOption>();
    public IReadOnlyList<ReportKpi> Kpis { get; set; } = Array.Empty<ReportKpi>();
    public IReadOnlyList<ReportColumn> Columns { get; set; } = Array.Empty<ReportColumn>();
    public IReadOnlyList<ReportRow> Rows { get; set; } = Array.Empty<ReportRow>();
    public int TotalRows { get; set; }
    public IReadOnlyList<string> ChartLabels { get; set; } = Array.Empty<string>();
    public IReadOnlyList<double> ChartValues { get; set; } = Array.Empty<double>();
    public string ChartType { get; set; } = "bar";
}

public sealed record ReportOption(string Value, string Label);
public sealed record ReportKpi(string Label, string Value);
public sealed record ReportColumn(string Header);
public sealed record ReportRow(IReadOnlyList<string> Cells);
