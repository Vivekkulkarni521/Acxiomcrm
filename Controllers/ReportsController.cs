using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace ACXIOMCRM.Controllers;

[Authorize]
public sealed class ReportsController : Controller
{
    private const int MaximumDisplayedRows = 500;
    private readonly ApplicationDbContext _db;
    private readonly CrmAccessService _access;
    private readonly CrmPermissionService _permissions;

    public ReportsController(ApplicationDbContext db, CrmAccessService access, CrmPermissionService permissions)
    {
        _db = db;
        _access = access;
        _permissions = permissions;
    }

    [CrmPermission("Reports", "View")]
    public async Task<IActionResult> Index(string type = "customers", string? search = null,
        DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        if (!IsKnownReport(type)) return BadRequest("Select a valid report.");
        if (!await CanViewReportAsync(type)) return Forbid();
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value.Date > dateTo.Value.Date)
            ModelState.AddModelError(string.Empty, "Start date must not be after end date.");
        return View(await BuildReportAsync(type, search, dateFrom, dateTo, export: false));
    }

    [HttpGet, CrmPermission("Reports", "View")]
    public async Task<IActionResult> ExportCsv(string type, string? search = null, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        if (!IsKnownReport(type)) return BadRequest("Select a valid report.");
        if (!await CanViewReportAsync(type)) return Forbid();
        var report = await BuildReportAsync(type, search, dateFrom, dateTo, export: true);
        var builder = new StringBuilder();
        AppendCsvLine(builder, [report.Title]);
        foreach (var kpi in report.Kpis) AppendCsvLine(builder, [kpi.Label, kpi.Value]);
        builder.AppendLine();
        AppendCsvLine(builder, report.Columns.Select(column => column.Header));
        foreach (var row in report.Rows) AppendCsvLine(builder, row.Cells);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"{type}-report-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet, CrmPermission("Reports", "View")]
    public async Task<IActionResult> ExportPdf(string type, string? search = null, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        if (!IsKnownReport(type)) return BadRequest("Select a valid report.");
        if (!await CanViewReportAsync(type)) return Forbid();
        var report = await BuildReportAsync(type, search, dateFrom, dateTo, export: true);
        var pdf = PdfReportWriter.Write(report);
        return File(pdf, "application/pdf", $"{type}-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    private async Task<ReportsViewModel> BuildReportAsync(string type, string? search, DateTime? dateFrom, DateTime? dateTo, bool export)
    {
        var model = new ReportsViewModel
        {
            Type = type,
            Search = search?.Trim(),
            DateFrom = dateFrom,
            DateTo = dateTo,
            ReportOptions = await ReportOptionsAsync()
        };
        var limit = export ? int.MaxValue : MaximumDisplayedRows;
        var term = model.Search;

        switch (type)
        {
            case "customers":
            {
                IQueryable<Customer> query = (await _access.CustomersForAsync(User)).AsNoTracking().Include(item => item.AssignedToUser);
                if (dateFrom.HasValue) query = query.Where(item => item.CreatedAt >= dateFrom.Value.Date);
                if (dateTo.HasValue) query = query.Where(item => item.CreatedAt < dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Name.Contains(term) || item.Email.Contains(term) || item.Company.Contains(term) || item.Phone.Contains(term));
                model.Title = "Customer Report";
                var total = await query.CountAsync();
                model.Kpis = [Kpi("Customers", total.ToString("N0"))];
                model.ChartLabels = await query.GroupBy(item => string.IsNullOrWhiteSpace(item.Company) ? "Unspecified" : item.Company)
                    .OrderByDescending(group => group.Count()).Take(8).Select(group => group.Key).ToListAsync();
                model.ChartValues = await query.GroupBy(item => string.IsNullOrWhiteSpace(item.Company) ? "Unspecified" : item.Company)
                    .OrderByDescending(group => group.Count()).Take(8).Select(group => (double)group.Count()).ToListAsync();
                model.ChartType = "doughnut";
                model.Columns = Columns("Name", "Email", "Phone", "Company", "City", "Assigned employee", "Created");
                var items = await query.OrderByDescending(item => item.CreatedAt).Take(limit).ToListAsync();
                model.Rows = items.Select(item => Row(item.Name, item.Email, item.Phone, item.Company, item.City,
                    item.AssignedToUser?.DisplayName ?? "Unassigned", item.CreatedAt.ToString("u"))).ToList();
                model.TotalRows = total;
                break;
            }
            case "leads":
            {
                IQueryable<Lead> query = (await _access.LeadsForAsync(User)).AsNoTracking().Include(item => item.AssignedToUser);
                if (dateFrom.HasValue) query = query.Where(item => item.CreatedAt >= dateFrom.Value.Date);
                if (dateTo.HasValue) query = query.Where(item => item.CreatedAt < dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Name.Contains(term) || item.Email.Contains(term) || item.Company.Contains(term) || item.Phone.Contains(term));
                model.Title = "Lead Report";
                var total = await query.CountAsync();
                var converted = await query.CountAsync(item => item.Status == LeadStatuses.Converted);
                var qualified = await query.CountAsync(item => item.Status == LeadStatuses.Qualified);
                model.Kpis = [Kpi("Leads", total.ToString("N0")), Kpi("Qualified", qualified.ToString("N0")), Kpi("Converted", converted.ToString("N0"))];
                var groups = await query.GroupBy(item => item.Status).Select(group => new { Status = group.Key, Count = group.Count() }).OrderBy(group => group.Status).ToListAsync();
                model.ChartLabels = groups.Select(group => group.Status).ToList();
                model.ChartValues = groups.Select(group => (double)group.Count).ToList();
                model.ChartType = "doughnut";
                model.Columns = Columns("Name", "Email", "Phone", "Company", "Source", "Status", "Assigned employee", "Created");
                var items = await query.OrderByDescending(item => item.CreatedAt).Take(limit).ToListAsync();
                model.Rows = items.Select(item => Row(item.Name, item.Email, item.Phone, item.Company, item.Source, item.Status,
                    item.AssignedToUser?.DisplayName ?? "Unassigned", item.CreatedAt.ToString("u"))).ToList();
                model.TotalRows = total;
                break;
            }
            case "followups":
            {
                IQueryable<FollowUp> query = (await _access.FollowUpsForAsync(User)).AsNoTracking().Include(item => item.Lead);
                if (dateFrom.HasValue) query = query.Where(item => item.ScheduledDate >= dateFrom.Value.Date);
                if (dateTo.HasValue) query = query.Where(item => item.ScheduledDate < dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Subject.Contains(term) || item.Description.Contains(term) || item.Lead!.Name.Contains(term));
                model.Title = "Follow-up Report";
                var total = await query.CountAsync();
                var planned = await query.CountAsync(item => item.Status == FollowUpStatus.Planned);
                var overdue = await query.CountAsync(item => item.Status == FollowUpStatus.Planned && item.ScheduledDate < DateTime.Now);
                var completed = await query.CountAsync(item => item.Status == FollowUpStatus.Completed);
                var missed = await query.CountAsync(item => item.Status == FollowUpStatus.Missed);
                model.Kpis = [Kpi("Follow-ups", total.ToString("N0")), Kpi("Planned", planned.ToString("N0")), Kpi("Overdue", overdue.ToString("N0")), Kpi("Completed", completed.ToString("N0")), Kpi("Missed", missed.ToString("N0"))];
                var groups = await query.GroupBy(item => item.Status).Select(group => new { Status = group.Key, Count = group.Count() }).OrderBy(group => group.Status).ToListAsync();
                model.ChartLabels = groups.SelectMany(group => group.Status == FollowUpStatus.Planned
                    ? new[] { "Planned", "Overdue" } : new[] { group.Status.ToString() }).ToList();
                model.ChartValues = groups.SelectMany(group => group.Status == FollowUpStatus.Planned
                    ? new[] { (double)(group.Count - overdue), (double)overdue } : new[] { (double)group.Count }).ToList();
                model.Columns = Columns("Lead", "Subject", "Scheduled", "Status", "Created");
                var items = await query.OrderByDescending(item => item.ScheduledDate).Take(limit).ToListAsync();
                model.Rows = items.Select(item => Row(item.Lead?.Name ?? "", item.Subject, item.ScheduledDate.ToString("yyyy-MM-dd HH:mm"), item.Status.ToString(), item.CreatedAt.ToString("u"))).ToList();
                model.TotalRows = total;
                break;
            }
            case "opportunities":
            {
                IQueryable<Opportunity> query = (await _access.OpportunitiesForAsync(User)).AsNoTracking().Include(item => item.Lead);
                if (dateFrom.HasValue) query = query.Where(item => item.ExpectedCloseDate >= dateFrom.Value.Date);
                if (dateTo.HasValue) query = query.Where(item => item.ExpectedCloseDate < dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Title.Contains(term) || item.Lead!.Name.Contains(term) || item.Lead.Company.Contains(term));
                model.Title = "Opportunity Pipeline Report";
                var total = await query.CountAsync();
                var pipeline = await query.Where(item => item.Stage == OpportunityStage.Qualification || item.Stage == OpportunityStage.Proposal || item.Stage == OpportunityStage.Negotiation).SumAsync(item => (decimal?)item.Amount) ?? 0;
                var won = await query.Where(item => item.Stage == OpportunityStage.Won).SumAsync(item => (decimal?)item.Amount) ?? 0;
                var groups = await query.GroupBy(item => item.Stage).Select(group => new { Stage = group.Key, Value = group.Sum(item => item.Amount) }).OrderBy(group => group.Stage).ToListAsync();
                model.Kpis = [Kpi("Opportunities", total.ToString("N0")), Kpi("Open pipeline", pipeline.ToString("C")), Kpi("Won revenue", won.ToString("C"))];
                model.ChartLabels = groups.Select(group => group.Stage.ToString()).ToList();
                model.ChartValues = groups.Select(group => (double)group.Value).ToList();
                model.Columns = Columns("Lead", "Opportunity", "Amount", "Stage", "Probability", "Expected close");
                var items = await query.OrderBy(item => item.ExpectedCloseDate).Take(limit).ToListAsync();
                model.Rows = items.Select(item => Row(item.Lead?.Name ?? "", item.Title, item.Amount.ToString("C", CultureInfo.CurrentCulture), item.Stage.ToString(), item.Probability + "%", item.ExpectedCloseDate.ToString("yyyy-MM-dd"))).ToList();
                model.TotalRows = total;
                break;
            }
            case "conversion":
            {
                IQueryable<Lead> leads = (await _access.LeadsForAsync(User)).AsNoTracking().Include(item => item.ConvertedCustomer);
                if (dateFrom.HasValue) leads = leads.Where(item => item.CreatedAt >= dateFrom.Value.Date);
                if (dateTo.HasValue) leads = leads.Where(item => item.CreatedAt < dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(term)) leads = leads.Where(item => item.Name.Contains(term) || item.Email.Contains(term) || item.Company.Contains(term));
                var accessibleCustomerIds = (await _access.CustomersForAsync(User)).Select(item => item.Id);
                var total = await leads.CountAsync();
                var converted = await leads.CountAsync(item => item.Status == LeadStatuses.Converted && item.ConvertedCustomer != null && accessibleCustomerIds.Contains(item.ConvertedCustomer.Id));
                var rate = total == 0 ? 0 : (double)converted * 100 / total;
                model.Title = "Lead Conversion Report";
                model.Kpis = [Kpi("Leads in period", total.ToString("N0")), Kpi("Converted", converted.ToString("N0")), Kpi("Conversion rate", rate.ToString("0.0") + "%")];
                model.ChartLabels = ["Converted", "Not converted"];
                model.ChartValues = [converted, Math.Max(0, total - converted)];
                model.ChartType = "doughnut";
                model.Columns = Columns("Lead", "Email", "Company", "Status", "Customer", "Lead created", "Converted customer created");
                var items = await leads.OrderByDescending(item => item.CreatedAt).Take(limit).ToListAsync();
                var visibleCustomerIds = await accessibleCustomerIds.ToHashSetAsync();
                model.Rows = items.Select(item =>
                {
                    var customer = item.ConvertedCustomer != null && visibleCustomerIds.Contains(item.ConvertedCustomer.Id) ? item.ConvertedCustomer : null;
                    return Row(item.Name, item.Email, item.Company, item.Status, customer?.Name ?? "", item.CreatedAt.ToString("u"), customer?.CreatedAt.ToString("u") ?? "");
                }).ToList();
                model.TotalRows = total;
                break;
            }
            case "activities":
            {
                IQueryable<CrmActivity> query = (await _access.ActivitiesForAsync(User)).AsNoTracking()
                    .Include(item => item.Customer).Include(item => item.Lead).Include(item => item.Opportunity).Include(item => item.AssignedToUser);
                if (dateFrom.HasValue) query = query.Where(item => item.ScheduledAt >= dateFrom.Value.Date);
                if (dateTo.HasValue) query = query.Where(item => item.ScheduledAt < dateTo.Value.Date.AddDays(1));
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Subject.Contains(term) || item.Description.Contains(term) || item.Customer!.Name.Contains(term) || item.Lead!.Name.Contains(term) || item.Opportunity!.Title.Contains(term));
                model.Title = "User Activity Report";
                var total = await query.CountAsync();
                var overdue = await query.CountAsync(item => item.Status == ActivityStatus.Planned && item.ScheduledAt < DateTime.Now);
                var completed = await query.CountAsync(item => item.Status == ActivityStatus.Completed);
                var groups = await query.GroupBy(item => item.Type).Select(group => new { Type = group.Key, Count = group.Count() }).OrderBy(group => group.Type).ToListAsync();
                model.Kpis = [Kpi("Activities", total.ToString("N0")), Kpi("Completed", completed.ToString("N0")), Kpi("Overdue", overdue.ToString("N0"))];
                model.ChartLabels = groups.Select(group => group.Type.ToString()).ToList();
                model.ChartValues = groups.Select(group => (double)group.Count).ToList();
                model.ChartType = "doughnut";
                model.Columns = Columns("Subject", "Type", "Related record", "Scheduled", "Status", "Priority", "Assigned employee");
                var items = await query.OrderByDescending(item => item.ScheduledAt).Take(limit).ToListAsync();
                model.Rows = items.Select(item => Row(item.Subject, item.Type.ToString(), item.Customer?.Name ?? item.Lead?.Name ?? item.Opportunity?.Title ?? "", item.ScheduledAt.ToString("yyyy-MM-dd HH:mm"), item.Status.ToString(), item.Priority.ToString(), item.AssignedToUser?.DisplayName ?? "")).ToList();
                model.TotalRows = total;
                break;
            }
        }
        return model;
    }

    private async Task<bool> CanViewReportAsync(string type)
    {
        if (!await _permissions.HasPermissionAsync(User, "Reports", "View")) return false;
        var module = type switch
        {
            "customers" => "Customers",
            "leads" or "conversion" => "Leads",
            "followups" => "FollowUps",
            "opportunities" => "Opportunities",
            "activities" => "Activities",
            _ => string.Empty
        };
        if (module.Length == 0 || !await _permissions.HasPermissionAsync(User, module, "View")) return false;
        return type != "conversion" || await _permissions.HasPermissionAsync(User, "Customers", "View");
    }

    private async Task<IReadOnlyList<ReportOption>> ReportOptionsAsync()
    {
        var candidates = new List<(string Type, string Label, string Module, bool NeedsCustomers)>
        {
            ("customers", "Customer Report", "Customers", false),
            ("leads", "Lead Report", "Leads", false),
            ("followups", "Follow-up Report", "FollowUps", false),
            ("opportunities", "Opportunity Pipeline Report", "Opportunities", false),
            ("conversion", "Conversion Report", "Leads", true),
            ("activities", "User Activity Report", "Activities", false)
        };
        var options = new List<ReportOption>();
        foreach (var item in candidates)
        {
            if (!await _permissions.HasPermissionAsync(User, item.Module, "View")) continue;
            if (item.NeedsCustomers && !await _permissions.HasPermissionAsync(User, "Customers", "View")) continue;
            options.Add(new ReportOption(item.Type, item.Label));
        }
        return options;
    }

    private static bool IsKnownReport(string type) =>
        new[] { "customers", "leads", "followups", "opportunities", "conversion", "activities" }
            .Contains(type, StringComparer.Ordinal);

    private static ReportKpi Kpi(string label, string value) => new(label, value);
    private static IReadOnlyList<ReportColumn> Columns(params string[] names) => names.Select(name => new ReportColumn(name)).ToList();
    private static ReportRow Row(params string?[] values) => new(values.Select(value => value ?? string.Empty).ToList());

    private static void AppendCsvLine(StringBuilder builder, IEnumerable<string> cells)
    {
        builder.AppendLine(string.Join(',', cells.Select(CsvCell)));
    }

    private static string CsvCell(string value)
    {
        var safe = value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
            value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}

