namespace ACXIOMCRM.Models;

public static class LeadStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string Unqualified = "Unqualified";
    public const string Converted = "Converted";
    public const string Lost = "Lost";

    public static readonly string[] All = [New, Contacted, Qualified, Unqualified, Converted, Lost];
    public static readonly string[] Selectable = [New, Contacted, Qualified, Unqualified, Lost];

    public static bool IsValid(string? status) => status is not null && All.Contains(status, StringComparer.Ordinal);
}
