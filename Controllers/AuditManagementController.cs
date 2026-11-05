using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ACXIOMCRM.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AuditManagementController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AuditManagementController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? department, string? auditorUserId,
        ManualAuditStatus? status, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!ValidDates(dateFrom, dateTo)) return BadRequest("Start date must not be after end date.");
        var query = _db.ManualAudits.AsNoTracking().Include(audit => audit.Auditor).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(audit => audit.Title.Contains(term) || audit.Department.Contains(term) ||
                audit.Description.Contains(term) || audit.Findings.Contains(term) || audit.AuditorName.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(audit => audit.Department == department);
        if (!string.IsNullOrWhiteSpace(auditorUserId)) query = query.Where(audit => audit.AuditorUserId == auditorUserId);
        if (status.HasValue) query = query.Where(audit => audit.Status == status.Value);
        if (dateFrom.HasValue) query = query.Where(audit => audit.AuditDate >= dateFrom.Value.Date);
        if (dateTo.HasValue) query = query.Where(audit => audit.AuditDate < dateTo.Value.Date.AddDays(1));

        var auditorUsers = await _users.Users.OrderBy(user => user.DisplayName)
            .Select(user => new { user.Id, user.DisplayName, user.Email }).ToListAsync();
        return View(new ManualAuditListViewModel
        {
            Audits = await query.OrderByDescending(audit => audit.AuditDate).ThenByDescending(audit => audit.Id).ToListAsync(),
            Departments = await _db.ManualAudits.AsNoTracking().Select(audit => audit.Department).Distinct().OrderBy(value => value).ToListAsync(),
            Auditors = auditorUsers.Select(user => new SelectListItem($"{user.DisplayName} ({user.Email})", user.Id, user.Id == auditorUserId)).ToList(),
            Search = search,
            Department = department,
            AuditorUserId = auditorUserId,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo
        });
    }

    [HttpGet]
    public async Task<IActionResult> Report(string? search, string? userName, string? auditorName, string? eventAction,
        string? status, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!ValidDates(dateFrom, dateTo)) return BadRequest("Start date must not be after end date.");
        return View("Report", await BuildReportAsync(search, userName, auditorName, eventAction, status, dateFrom, dateTo));
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? search, string? userName, string? auditorName, string? eventAction,
        string? status, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!ValidDates(dateFrom, dateTo)) return BadRequest("Start date must not be after end date.");
        var model = await BuildReportAsync(search, userName, auditorName, eventAction, status, dateFrom, dateTo);
        var csv = new StringBuilder();
        AppendCsv(csv, ["Manual Audit Report", "Generated (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")]);
        AppendCsv(csv, ["Total events", "Created", "Updated", "Deleted"]);
        AppendCsv(csv, [N(model.Total), N(model.Created), N(model.Updated), N(model.Deleted)]);
        csv.AppendLine();
        AppendCsv(csv, ["Timestamp (UTC)", "Action", "Changed by", "Audit record ID", "Title", "Department", "Auditor", "Audit date", "Status", "Description", "Findings"]);
        foreach (var item in model.Events)
            AppendCsv(csv, [item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), item.Action, item.UserName, item.AuditRecordId.ToString(),
                item.Title, item.Department, item.AuditorName, item.AuditDate.ToString("yyyy-MM-dd"), item.Status, item.Description, item.Findings]);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"manual-audit-report-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(string? search, string? userName, string? auditorName, string? eventAction,
        string? status, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!ValidDates(dateFrom, dateTo)) return BadRequest("Start date must not be after end date.");
        var model = await BuildReportAsync(search, userName, auditorName, eventAction, status, dateFrom, dateTo);
        var rows = new List<ReportRow>
        {
            new(["TOTAL EVENTS", N(model.Total), "", "", "", "", "", "", "", "", ""]),
            new(["CREATED", N(model.Created), "", "", "", "", "", "", "", "", ""]),
            new(["UPDATED", N(model.Updated), "", "", "", "", "", "", "", "", ""]),
            new(["DELETED", N(model.Deleted), "", "", "", "", "", "", "", "", ""]),
            new(["RECORDED AUDIT CHANGES", "", "", "", "", "", "", "", "", "", ""])
        };
        rows.AddRange(model.Events.Select(item => new ReportRow([
            item.Timestamp.ToString("u"), item.Action, item.UserName, item.AuditRecordId.ToString(), item.Title,
            item.Department, item.AuditorName, item.AuditDate.ToString("yyyy-MM-dd"), item.Status, item.Description, item.Findings])));
        var report = new ReportsViewModel
        {
            Title = "Manual Audit Report",
            Search = model.Search,
            DateFrom = dateFrom,
            DateTo = dateTo,
            TotalRows = model.Total,
            Kpis = [new ReportKpi("Events", N(model.Total)), new ReportKpi("Created", N(model.Created)),
                new ReportKpi("Updated", N(model.Updated)), new ReportKpi("Deleted", N(model.Deleted))],
            Columns = [new ReportColumn("Timestamp"), new ReportColumn("Action"), new ReportColumn("Changed by"),
                new ReportColumn("Record ID"), new ReportColumn("Title"), new ReportColumn("Department"),
                new ReportColumn("Auditor"), new ReportColumn("Audit date"), new ReportColumn("Status"),
                new ReportColumn("Description"), new ReportColumn("Findings")],
            Rows = rows
        };
        return File(PdfReportWriter.Write(report), "application/pdf", $"manual-audit-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var audit = await _db.ManualAudits.AsNoTracking().Include(item => item.Auditor).FirstOrDefaultAsync(item => item.Id == id);
        return audit == null ? NotFound() : View(audit);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ManualAuditFormViewModel();
        await PopulateAuditorsAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManualAuditFormViewModel model)
    {
        await ValidateAndPopulateAuditorAsync(model);
        if (model.AuditDate == default) ModelState.AddModelError(nameof(model.AuditDate), "Enter a valid audit date.");
        if (!ModelState.IsValid) return View(model);

        var audit = new ManualAudit
        {
            Title = model.Title.Trim(),
            Department = model.Department.Trim(),
            AuditorUserId = model.AuditorUserId,
            AuditorName = await AuditorNameAsync(model.AuditorUserId),
            AuditDate = model.AuditDate.Date,
            Description = model.Description.Trim(),
            Findings = model.Findings.Trim(),
            Status = model.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.ManualAudits.Add(audit);
        await _db.SaveChangesAsync();
        _db.ManualAuditEvents.Add(await SnapshotAsync(audit, "Create"));
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["Success"] = "Audit created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var audit = await _db.ManualAudits.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (audit == null) return NotFound();
        var model = new ManualAuditFormViewModel
        {
            Id = audit.Id,
            Title = audit.Title,
            Department = audit.Department,
            AuditorUserId = audit.AuditorUserId ?? string.Empty,
            AuditDate = audit.AuditDate,
            Description = audit.Description,
            Findings = audit.Findings,
            Status = audit.Status
        };
        await PopulateAuditorsAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ManualAuditFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        await ValidateAndPopulateAuditorAsync(model);
        if (model.AuditDate == default) ModelState.AddModelError(nameof(model.AuditDate), "Enter a valid audit date.");
        if (!ModelState.IsValid) return View(model);
        var audit = await _db.ManualAudits.FirstOrDefaultAsync(item => item.Id == id);
        if (audit == null) return NotFound();

        audit.Title = model.Title.Trim();
        audit.Department = model.Department.Trim();
        audit.AuditorUserId = model.AuditorUserId;
        audit.AuditorName = await AuditorNameAsync(model.AuditorUserId);
        audit.AuditDate = model.AuditDate.Date;
        audit.Description = model.Description.Trim();
        audit.Findings = model.Findings.Trim();
        audit.Status = model.Status;
        audit.UpdatedAt = DateTime.UtcNow;
        _db.ManualAuditEvents.Add(await SnapshotAsync(audit, "Update"));
        await _db.SaveChangesAsync();
        TempData["Success"] = "Audit updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var audit = await _db.ManualAudits.AsNoTracking().Include(item => item.Auditor).FirstOrDefaultAsync(item => item.Id == id);
        return audit == null ? NotFound() : View(audit);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var audit = await _db.ManualAudits.Include(item => item.Auditor).FirstOrDefaultAsync(item => item.Id == id);
        if (audit == null) return NotFound();
        _db.ManualAuditEvents.Add(await SnapshotAsync(audit, "Delete"));
        _db.ManualAudits.Remove(audit);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Audit deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ManualAuditEvent> SnapshotAsync(ManualAudit audit, string action)
    {
        var auditor = await _users.FindByIdAsync(audit.AuditorUserId ?? string.Empty);
        var actor = User.Identity?.Name ?? "Unknown";
        return new ManualAuditEvent
        {
            AuditRecordId = audit.Id,
            Action = action,
            UserName = actor,
            Title = audit.Title,
            Department = audit.Department,
            AuditorUserId = audit.AuditorUserId ?? string.Empty,
            AuditorName = auditor?.DisplayName is { Length: > 0 } displayName ? displayName : auditor?.Email ?? audit.AuditorName,
            AuditDate = audit.AuditDate,
            Description = audit.Description,
            Findings = audit.Findings,
            Status = audit.Status.ToString(),
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task ValidateAndPopulateAuditorAsync(ManualAuditFormViewModel model)
    {
        var selected = await _users.FindByIdAsync(model.AuditorUserId);
        if (selected == null || !selected.IsActive)
            ModelState.AddModelError(nameof(model.AuditorUserId), "Select an active auditor account.");
        await PopulateAuditorsAsync(model);
    }

    private async Task PopulateAuditorsAsync(ManualAuditFormViewModel model)
    {
        var users = await _users.Users.Where(user => user.IsActive).OrderBy(user => user.DisplayName)
            .Select(user => new { user.Id, user.DisplayName, user.Email }).ToListAsync();
        model.Auditors = users.Select(user => new SelectListItem($"{user.DisplayName} ({user.Email})", user.Id,
            user.Id == model.AuditorUserId)).ToList();
    }

    private async Task<string> AuditorNameAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        return user?.DisplayName is { Length: > 0 } name ? name : user?.Email ?? user?.UserName ?? userId;
    }

    private async Task<ManualAuditReportViewModel> BuildReportAsync(string? search, string? userName, string? auditorName,
        string? action, string? status, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = _db.ManualAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Title.Contains(term) || item.Department.Contains(term) || item.AuditorName.Contains(term) ||
                item.UserName.Contains(term) || item.Action.Contains(term) || item.Status.Contains(term) ||
                item.Description.Contains(term) || item.Findings.Contains(term) || item.AuditRecordId.ToString().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(userName)) query = query.Where(item => item.UserName == userName);
        if (!string.IsNullOrWhiteSpace(auditorName)) query = query.Where(item => item.AuditorName == auditorName);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(item => item.Action == action);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        if (dateFrom.HasValue) query = query.Where(item => item.Timestamp >= dateFrom.Value.Date);
        if (dateTo.HasValue) query = query.Where(item => item.Timestamp < dateTo.Value.Date.AddDays(1));

        return new ManualAuditReportViewModel
        {
            Events = await query.OrderByDescending(item => item.Timestamp).ThenByDescending(item => item.Id).ToListAsync(),
            Users = await _db.ManualAuditEvents.AsNoTracking().Select(item => item.UserName).Distinct().OrderBy(value => value).ToListAsync(),
            Auditors = await _db.ManualAuditEvents.AsNoTracking().Select(item => item.AuditorName).Distinct().OrderBy(value => value).ToListAsync(),
            Actions = await _db.ManualAuditEvents.AsNoTracking().Select(item => item.Action).Distinct().OrderBy(value => value).ToListAsync(),
            Statuses = await _db.ManualAuditEvents.AsNoTracking().Select(item => item.Status).Distinct().OrderBy(value => value).ToListAsync(),
            Search = search,
            UserName = userName,
            AuditorName = auditorName,
            Action = action,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Total = await query.CountAsync(),
            Created = await query.CountAsync(item => item.Action == "Create"),
            Updated = await query.CountAsync(item => item.Action == "Update"),
            Deleted = await query.CountAsync(item => item.Action == "Delete")
        };
    }

    private static bool ValidDates(DateTime? from, DateTime? to) =>
        !from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date;

    private static string N(int value) => value.ToString("N0");

    private static void AppendCsv(StringBuilder output, IEnumerable<string> values) =>
        output.AppendLine(string.Join(',', values.Select(value => "\"" + CsvSafe(value).Replace("\"", "\"\"") + "\"")));

    private static string CsvSafe(string value) =>
        value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
        value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') ? "'" + value : value;
}
