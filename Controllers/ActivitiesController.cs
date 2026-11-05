using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ACXIOMCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessService _access;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(ApplicationDbContext context, CrmAccessService access,
        UserManager<ApplicationUser> userManager, ILogger<ActivitiesController> logger)
    {
        _context = context;
        _access = access;
        _userManager = userManager;
        _logger = logger;
    }

    [CrmPermission("Activities", "View")]
    public async Task<IActionResult> Index(string? search, ActivityType? type, ActivityStatus? status,
        DateTime? dateFrom, DateTime? dateTo)
    {
        if ((type.HasValue && !Enum.IsDefined(type.Value)) || (status.HasValue && !Enum.IsDefined(status.Value)))
            return BadRequest("Invalid activity filter.");
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value.Date > dateTo.Value.Date)
            return BadRequest("Start date must be on or before end date.");

        var now = DateTime.Now;
        var query = (await _access.ActivitiesForAsync(User)).AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .Include(a => a.AssignedToUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Subject.Contains(term) || a.Description.Contains(term) ||
                (a.Customer != null && a.Customer.Name.Contains(term)) ||
                (a.Lead != null && a.Lead.Name.Contains(term)) ||
                (a.Opportunity != null && a.Opportunity.Title.Contains(term)));
        }
        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);
        if (status.HasValue)
        {
            query = status.Value == ActivityStatus.Overdue
                ? query.Where(a => a.Status == ActivityStatus.Overdue ||
                    (a.Status == ActivityStatus.Planned && a.ScheduledAt < now))
                : status.Value == ActivityStatus.Planned
                    ? query.Where(a => a.Status == ActivityStatus.Planned && a.ScheduledAt >= now)
                    : query.Where(a => a.Status == status.Value);
        }
        if (dateFrom.HasValue)
            query = query.Where(a => a.ScheduledAt >= dateFrom.Value.Date);
        if (dateTo.HasValue)
            query = query.Where(a => a.ScheduledAt < dateTo.Value.Date.AddDays(1));

        var items = await query.OrderBy(a => a.ScheduledAt).ToListAsync();
        var model = new ActivityListViewModel
        {
            Activities = items.Select(activity => new ActivityRowViewModel
            {
                Activity = activity,
                DisplayStatus = GetDisplayStatus(activity, now)
            }).ToList(),
            Search = search,
            Type = type,
            Status = status,
            DateFrom = dateFrom,
            DateTo = dateTo
        };
        return View(model);
    }

    [HttpGet]
    [CrmPermission("Activities", "Create")]
    public async Task<IActionResult> Create()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var activity = new CrmActivity { ScheduledAt = DateTime.Now.AddHours(1), AssignedToUserId = userId };
        await PopulateFormOptionsAsync(activity);
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Activities", "Create")]
    public async Task<IActionResult> Create(CrmActivity activity)
    {
        await ValidateRelationshipsAsync(activity);
        await SetOrValidateAssigneeAsync(activity);
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(activity);
            return View(activity);
        }

        try
        {
            activity.CreatedAt = DateTime.UtcNow;
            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Activity created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Unable to create CRM activity.");
            ModelState.AddModelError(string.Empty, "The activity could not be saved. Please try again.");
            await PopulateFormOptionsAsync(activity);
            return View(activity);
        }
    }

    [HttpGet]
    [CrmPermission("Activities", "View")]
    public async Task<IActionResult> Details(int id)
    {
        var activity = await (await LoadActivityQueryAsync()).AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
            return NotFound();
        return View(new ActivityDetailsViewModel { Activity = activity, DisplayStatus = GetDisplayStatus(activity, DateTime.Now) });
    }

    [HttpGet]
    [CrmPermission("Activities", "Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var activity = await (await LoadActivityQueryAsync()).FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
            return NotFound();
        await PopulateFormOptionsAsync(activity);
        return View(activity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Activities", "Edit")]
    public async Task<IActionResult> Edit(int id, CrmActivity activity)
    {
        if (id != activity.Id)
            return BadRequest();

        var existing = await (await _access.ActivitiesForAsync(User)).FirstOrDefaultAsync(a => a.Id == id);
        if (existing == null)
            return NotFound();

        await ValidateRelationshipsAsync(activity);
        await SetOrValidateAssigneeAsync(activity, existing);
        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(activity, existing.AssignedToUser?.DisplayName);
            return View(activity);
        }

        existing.Type = activity.Type;
        existing.Subject = activity.Subject;
        existing.Description = activity.Description;
        existing.ScheduledAt = activity.ScheduledAt;
        existing.Status = activity.Status;
        existing.Priority = activity.Priority;
        existing.CustomerId = activity.CustomerId;
        existing.LeadId = activity.LeadId;
        existing.OpportunityId = activity.OpportunityId;
        if (User.IsInRole("Admin"))
            existing.AssignedToUserId = activity.AssignedToUserId;

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = "Activity updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Unable to update CRM activity {ActivityId}.", id);
            ModelState.AddModelError(string.Empty, "The activity could not be saved. Please try again.");
            await PopulateFormOptionsAsync(activity, existing.AssignedToUser?.DisplayName);
            return View(activity);
        }
    }

    [HttpGet]
    [CrmPermission("Activities", "Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await (await LoadActivityQueryAsync()).AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return activity == null ? NotFound() : View(activity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [CrmPermission("Activities", "Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await (await _access.ActivitiesForAsync(User)).FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
            return NotFound();
        try
        {
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Activity deleted successfully.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Unable to delete CRM activity {ActivityId}.", id);
            TempData["Error"] = "The activity could not be deleted. Please try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IQueryable<CrmActivity>> LoadActivityQueryAsync() =>
        (await _access.ActivitiesForAsync(User))
        .Include(a => a.Customer)
        .Include(a => a.Lead)
        .Include(a => a.Opportunity)
        .Include(a => a.AssignedToUser);

    private static ActivityStatus GetDisplayStatus(CrmActivity activity, DateTime now) =>
        activity.Status == ActivityStatus.Planned && activity.ScheduledAt < now
            ? ActivityStatus.Overdue
            : activity.Status;

    private async Task ValidateRelationshipsAsync(CrmActivity activity)
    {
        if (!Enum.IsDefined(activity.Type))
            ModelState.AddModelError(nameof(activity.Type), "Select a valid activity type.");
        if (!Enum.IsDefined(activity.Status))
            ModelState.AddModelError(nameof(activity.Status), "Select a valid activity status.");
        if (!Enum.IsDefined(activity.Priority))
            ModelState.AddModelError(nameof(activity.Priority), "Select a valid priority.");
        if (!activity.CustomerId.HasValue && !activity.LeadId.HasValue && !activity.OpportunityId.HasValue)
            ModelState.AddModelError(string.Empty, "Link this activity to a customer, lead, or opportunity.");

        Customer? customer = null;
        Lead? lead = null;
        Opportunity? opportunity = null;
        if (activity.CustomerId.HasValue)
        {
            customer = await (await _access.CustomersForAsync(User)).FirstOrDefaultAsync(c => c.Id == activity.CustomerId.Value);
            if (customer == null)
                ModelState.AddModelError(nameof(activity.CustomerId), "Select a customer you are allowed to access.");
        }
        if (activity.LeadId.HasValue)
        {
            lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(l => l.Id == activity.LeadId.Value);
            if (lead == null)
                ModelState.AddModelError(nameof(activity.LeadId), "Select a lead you are allowed to access.");
        }
        if (activity.OpportunityId.HasValue)
        {
            opportunity = await (await _access.OpportunitiesForAsync(User))
                .FirstOrDefaultAsync(o => o.Id == activity.OpportunityId.Value);
            if (opportunity == null)
                ModelState.AddModelError(nameof(activity.OpportunityId), "Select an opportunity you are allowed to access.");
        }
        if (lead != null && opportunity != null && lead.Id != opportunity.LeadId)
            ModelState.AddModelError(nameof(activity.LeadId), "The selected lead must match the selected opportunity's lead.");
        if (lead != null && customer?.ConvertedFromLeadId.HasValue == true && lead.Id != customer.ConvertedFromLeadId)
            ModelState.AddModelError(nameof(activity.CustomerId), "The selected customer was converted from a different lead.");
    }

    private async Task SetOrValidateAssigneeAsync(CrmActivity activity, CrmActivity? existing = null)
    {
        if (!User.IsInRole("Admin"))
        {
            activity.AssignedToUserId = existing?.AssignedToUserId ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return;
        }

        var assignee = string.IsNullOrWhiteSpace(activity.AssignedToUserId)
            ? null
            : await _userManager.FindByIdAsync(activity.AssignedToUserId);
        if (assignee == null || !assignee.IsActive)
        {
            ModelState.AddModelError(nameof(activity.AssignedToUserId), "Select an active employee.");
            return;
        }
        if (!(await _userManager.GetRolesAsync(assignee)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
            ModelState.AddModelError(nameof(activity.AssignedToUserId), "The selected employee does not have a CRM role.");
    }

    private async Task PopulateFormOptionsAsync(CrmActivity activity, string? existingAssigneeName = null)
    {
        var customers = await (await _access.CustomersForAsync(User)).AsNoTracking()
            .OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.Company }).ToListAsync();
        var leads = await (await _access.LeadsForAsync(User)).AsNoTracking()
            .OrderBy(l => l.Name).Select(l => new { l.Id, l.Name, l.Company }).ToListAsync();
        var opportunities = await (await _access.OpportunitiesForAsync(User)).AsNoTracking()
            .Include(o => o.Lead).OrderBy(o => o.Title)
            .Select(o => new { o.Id, o.Title, LeadName = o.Lead!.Name }).ToListAsync();

        ViewBag.Customers = new SelectList(customers.Select(c => new
        {
            c.Id,
            DisplayName = string.IsNullOrWhiteSpace(c.Company) ? c.Name : $"{c.Name} ({c.Company})"
        }), "Id", "DisplayName", activity.CustomerId);
        ViewBag.Leads = new SelectList(leads.Select(l => new
        {
            l.Id,
            DisplayName = string.IsNullOrWhiteSpace(l.Company) ? l.Name : $"{l.Name} ({l.Company})"
        }), "Id", "DisplayName", activity.LeadId);
        ViewBag.Opportunities = new SelectList(opportunities.Select(o => new
        {
            o.Id,
            DisplayName = $"{o.Title} ({o.LeadName})"
        }), "Id", "DisplayName", activity.OpportunityId);

        if (User.IsInRole("Admin"))
        {
            var activeUsers = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.DisplayName).ToListAsync();
            var employees = new List<SelectListItem>();
            foreach (var user in activeUsers)
            {
                if ((await _userManager.GetRolesAsync(user)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
                    employees.Add(new SelectListItem($"{user.DisplayName} ({user.Email})", user.Id, user.Id == activity.AssignedToUserId));
            }
            ViewBag.Employees = employees;
        }
        else
        {
            var user = await _userManager.FindByIdAsync(activity.AssignedToUserId);
            ViewBag.AssignedEmployeeName = existingAssigneeName ?? user?.DisplayName ?? User.Identity?.Name ?? "Current user";
        }
    }
}
