using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ACXIOMCRM.Controllers
{
    [Authorize]
    public class FollowUpsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CrmAccessService _access;
        private readonly ILogger<FollowUpsController> _logger;

        public FollowUpsController(ApplicationDbContext context, CrmAccessService access, ILogger<FollowUpsController> logger)
        {
            _context = context;
            _access = access;
            _logger = logger;
        }

        [CrmPermission("FollowUps", "View")]
        public async Task<IActionResult> Index(string? search, FollowUpStatus? status)
        {
            if (status.HasValue && !Enum.IsDefined(status.Value))
                return BadRequest("Invalid follow-up status filter.");

            var query = (await _access.FollowUpsForAsync(User))
                .AsNoTracking().Include(f => f.Lead).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(f => f.Subject.Contains(term) || f.Description.Contains(term) ||
                    (f.Lead != null && f.Lead.Name.Contains(term)));
            }
            if (status.HasValue)
                query = query.Where(f => f.Status == status.Value);

            var matching = await query.OrderBy(f => f.ScheduledDate).ToListAsync();
            var now = DateTime.Now;
            var model = new FollowUpListViewModel
            {
                UpcomingPlanned = matching.Where(f => f.Status == FollowUpStatus.Planned && f.ScheduledDate >= now).ToList(),
                OverduePlanned = matching.Where(f => f.Status == FollowUpStatus.Planned && f.ScheduledDate < now).ToList(),
                OtherFollowUps = matching.Where(f => f.Status != FollowUpStatus.Planned).ToList(),
                Search = search,
                Status = status
            };
            return View(model);
        }

        [HttpGet]
        [CrmPermission("FollowUps", "Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateLeadsAsync();
            return View(new FollowUp { ScheduledDate = DateTime.Now.AddHours(1) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("FollowUps", "Create")]
        public async Task<IActionResult> Create(FollowUp followUp)
        {
            if (!Enum.IsDefined(followUp.Status))
                ModelState.AddModelError(nameof(followUp.Status), "Select a valid status.");
            if (!await (await _access.LeadsForAsync(User)).AnyAsync(l => l.Id == followUp.LeadId))
                ModelState.AddModelError(nameof(followUp.LeadId), "Select a lead you are allowed to access.");

            if (!ModelState.IsValid)
            {
                await PopulateLeadsAsync(followUp.LeadId);
                return View(followUp);
            }

            try
            {
                followUp.CreatedAt = DateTime.UtcNow;
                _context.FollowUps.Add(followUp);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Follow-up created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unable to create follow-up for lead {LeadId}.", followUp.LeadId);
                ModelState.AddModelError(string.Empty, "The follow-up could not be saved. Please try again.");
                await PopulateLeadsAsync(followUp.LeadId);
                return View(followUp);
            }
        }

        [HttpGet]
        [CrmPermission("FollowUps", "View")]
        public async Task<IActionResult> Details(int id)
        {
            var followUp = await (await _access.FollowUpsForAsync(User)).AsNoTracking()
                .Include(f => f.Lead).FirstOrDefaultAsync(f => f.Id == id);
            return followUp == null ? NotFound() : View(followUp);
        }

        [HttpGet]
        [CrmPermission("FollowUps", "Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var followUp = await (await _access.FollowUpsForAsync(User)).FirstOrDefaultAsync(f => f.Id == id);
            if (followUp == null)
                return NotFound();
            await PopulateLeadsAsync(followUp.LeadId);
            return View(followUp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("FollowUps", "Edit")]
        public async Task<IActionResult> Edit(int id, FollowUp followUp)
        {
            if (id != followUp.Id)
                return BadRequest();
            if (!Enum.IsDefined(followUp.Status))
                ModelState.AddModelError(nameof(followUp.Status), "Select a valid status.");
            if (!await (await _access.LeadsForAsync(User)).AnyAsync(l => l.Id == followUp.LeadId))
                ModelState.AddModelError(nameof(followUp.LeadId), "Select a lead you are allowed to access.");

            if (!ModelState.IsValid)
            {
                await PopulateLeadsAsync(followUp.LeadId);
                return View(followUp);
            }

            var existing = await (await _access.FollowUpsForAsync(User)).FirstOrDefaultAsync(f => f.Id == id);
            if (existing == null)
                return NotFound();

            existing.LeadId = followUp.LeadId;
            existing.Subject = followUp.Subject;
            existing.Description = followUp.Description;
            existing.ScheduledDate = followUp.ScheduledDate;
            existing.Status = followUp.Status;
            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Follow-up updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unable to update follow-up {FollowUpId}.", id);
                ModelState.AddModelError(string.Empty, "The follow-up could not be saved. Please try again.");
                await PopulateLeadsAsync(followUp.LeadId);
                return View(followUp);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("FollowUps", "Edit")]
        public async Task<IActionResult> UpdateStatus(int id, FollowUpStatus status)
        {
            if (!Enum.IsDefined(status) || status == FollowUpStatus.Planned)
                return BadRequest("Invalid follow-up status.");
            var followUp = await (await _access.FollowUpsForAsync(User)).FirstOrDefaultAsync(f => f.Id == id);
            if (followUp == null)
                return NotFound();
            if (followUp.Status != FollowUpStatus.Planned)
            {
                TempData["Error"] = "Only Planned follow-ups can be marked Completed, Missed, or Cancelled.";
                return RedirectToAction(nameof(Index));
            }
            followUp.Status = status;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Follow-up marked {status}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [CrmPermission("FollowUps", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var followUp = await (await _access.FollowUpsForAsync(User)).AsNoTracking()
                .Include(f => f.Lead).FirstOrDefaultAsync(f => f.Id == id);
            return followUp == null ? NotFound() : View(followUp);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [CrmPermission("FollowUps", "Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var followUp = await (await _access.FollowUpsForAsync(User)).FirstOrDefaultAsync(f => f.Id == id);
            if (followUp == null)
                return NotFound();
            try
            {
                _context.FollowUps.Remove(followUp);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Follow-up deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unable to delete follow-up {FollowUpId}.", id);
                TempData["Error"] = "The follow-up could not be deleted. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLeadsAsync(int? selectedLeadId = null)
        {
            var leads = await (await _access.LeadsForAsync(User)).AsNoTracking()
                .OrderBy(l => l.Name).Select(l => new { l.Id, l.Name, l.Company }).ToListAsync();
            ViewBag.Leads = new SelectList(leads.Select(l => new
            {
                l.Id,
                DisplayName = string.IsNullOrWhiteSpace(l.Company) ? l.Name : $"{l.Name} ({l.Company})"
            }), "Id", "DisplayName", selectedLeadId);
        }
    }
}
