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
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessService _access;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeadsController(ApplicationDbContext context, CrmAccessService access, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _access = access;
        _userManager = userManager;
    }

    [CrmPermission("Leads", "View")]
    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = (await _access.LeadsForAsync(User)).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l => l.Name.Contains(term) || l.Email.Contains(term) ||
                l.Phone.Contains(term) || l.Company.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!LeadStatuses.IsValid(status))
                return BadRequest("Invalid lead status filter.");
            query = query.Where(l => l.Status == status);
        }

        var model = new LeadListViewModel
        {
            Leads = await query.Include(l => l.AssignedToUser)
                .OrderByDescending(l => l.CreatedAt).ToListAsync(),
            Search = search,
            Status = status
        };
        return View(model);
    }

    [HttpGet]
    [CrmPermission("Leads", "View")]
    public async Task<IActionResult> Details(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).AsNoTracking()
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .FirstOrDefaultAsync(l => l.Id == id);
        return lead == null ? NotFound() : View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Leads", "Edit")]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
            return NotFound();
        if (!LeadStatuses.Selectable.Contains(status, StringComparer.Ordinal))
        {
            TempData["Error"] = "Select a valid status. Use Convert for Qualified leads.";
            return RedirectToAction(nameof(Index));
        }
        if (lead.Status == LeadStatuses.Converted)
        {
            TempData["Error"] = "A converted lead cannot be changed to another status.";
            return RedirectToAction(nameof(Index));
        }

        lead.Status = status;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Lead status updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [CrmPermission("Leads", "Create")]
    public async Task<IActionResult> Create()
    {
        await PopulateAssigneesAsync();
        return View(new Lead { Status = LeadStatuses.New });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Leads", "Create")]
    public async Task<IActionResult> Create(Lead lead)
    {
        await SetOrValidateOwnerAsync(lead);
        ValidateEditableStatus(lead.Status);
        if (!ModelState.IsValid)
        {
            await PopulateAssigneesAsync(lead.AssignedToUserId);
            return View(lead);
        }

        lead.CreatedAt = DateTime.UtcNow;
        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Lead added successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [CrmPermission("Leads", "Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
            return NotFound();
        await PopulateAssigneesAsync(lead.AssignedToUserId);
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Leads", "Edit")]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.Id)
            return BadRequest();

        var existing = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(l => l.Id == id);
        if (existing == null)
            return NotFound();

        if (existing.Status == LeadStatuses.Converted)
        {
            if (lead.Status != LeadStatuses.Converted)
                ModelState.AddModelError(nameof(lead.Status), "A converted lead cannot be changed to another status.");
        }
        else
        {
            ValidateEditableStatus(lead.Status);
        }

        await SetOrValidateOwnerAsync(lead);
        if (!ModelState.IsValid)
        {
            await PopulateAssigneesAsync(lead.AssignedToUserId);
            return View(lead);
        }

        existing.Name = lead.Name;
        existing.Email = lead.Email;
        existing.Phone = lead.Phone;
        existing.Company = lead.Company;
        existing.Source = lead.Source;
        if (existing.Status != LeadStatuses.Converted)
            existing.Status = lead.Status;
        if (User.IsInRole("Admin"))
            existing.AssignedToUserId = lead.AssignedToUserId;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Lead updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [CrmPermission("Leads", "Edit")]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).AsNoTracking()
            .Include(l => l.AssignedToUser)
            .Include(l => l.ConvertedCustomer)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
            return NotFound();
        if (lead.Status != LeadStatuses.Qualified || lead.ConvertedCustomer != null)
            return BadRequest("Only an unconverted Qualified lead can be converted.");
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Leads", "Edit")]
    public async Task<IActionResult> ConvertConfirmed(int id)
    {
        var lead = await (await _access.LeadsForAsync(User))
            .Include(l => l.ConvertedCustomer).FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
            return NotFound();
        if (lead.Status != LeadStatuses.Qualified || lead.ConvertedCustomer != null)
        {
            TempData["Error"] = "Only an unconverted Qualified lead can be converted.";
            return RedirectToAction(nameof(Index));
        }

        var duplicate = await _context.Customers.AnyAsync(c => c.Email == lead.Email);
        if (duplicate)
        {
            TempData["Error"] = "A customer with this email already exists. The lead was not converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var customer = new Customer
        {
            Name = lead.Name,
            Email = lead.Email,
            Phone = lead.Phone,
            Company = lead.Company,
            AssignedToUserId = lead.AssignedToUserId,
            ConvertedFromLeadId = lead.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Customers.Add(customer);
        lead.Status = LeadStatuses.Converted;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.Entry(customer).State = EntityState.Detached;
            TempData["Error"] = "This lead has already been converted or could not be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = "Lead converted to a customer successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [CrmPermission("Leads", "Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        return lead == null ? NotFound() : View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [CrmPermission("Leads", "Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await (await _access.LeadsForAsync(User)).FirstOrDefaultAsync(l => l.Id == id);
        if (lead == null)
            return NotFound();

        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Lead deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateEditableStatus(string? status)
    {
        if (!LeadStatuses.Selectable.Contains(status ?? string.Empty, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(Lead.Status), "Select a valid status. Converted leads must use the conversion action.");
    }

    private async Task SetOrValidateOwnerAsync(Lead lead)
    {
        if (!User.IsInRole("Admin"))
        {
            lead.AssignedToUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return;
        }

        if (string.IsNullOrWhiteSpace(lead.AssignedToUserId))
            return;

        var assignee = await _userManager.FindByIdAsync(lead.AssignedToUserId);
        if (assignee == null || !assignee.IsActive)
            ModelState.AddModelError(nameof(lead.AssignedToUserId), "Select an active user.");
        else if (!(await _userManager.GetRolesAsync(assignee)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
            ModelState.AddModelError(nameof(lead.AssignedToUserId), "The selected user does not have a CRM role.");
    }

    private async Task PopulateAssigneesAsync(string? selectedUserId = null)
    {
        if (!User.IsInRole("Admin"))
            return;

        var activeUsers = await _userManager.Users.Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName).ToListAsync();
        var assignees = new List<SelectListItem>();
        foreach (var user in activeUsers)
        {
            if ((await _userManager.GetRolesAsync(user)).Any(role => role is "Admin" or "Manager" or "SalesExecutive"))
                assignees.Add(new SelectListItem($"{user.DisplayName} ({user.Email})", user.Id, user.Id == selectedUserId));
        }
        ViewBag.Assignees = assignees;
    }
}
