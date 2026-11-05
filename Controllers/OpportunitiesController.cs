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
    public class OpportunitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CrmAccessService _access;
        private readonly ILogger<OpportunitiesController> _logger;

        public OpportunitiesController(ApplicationDbContext context, CrmAccessService access, ILogger<OpportunitiesController> logger)
        {
            _context = context;
            _access = access;
            _logger = logger;
        }

        [CrmPermission("Opportunities", "View")]
        public async Task<IActionResult> Index(string? search, OpportunityStage? stage)
        {
            if (stage.HasValue && !Enum.IsDefined(stage.Value))
                return BadRequest("Invalid opportunity stage filter.");

            var visibleOpportunities = (await _access.OpportunitiesForAsync(User)).AsNoTracking();
            var groupedStages = await visibleOpportunities
                .GroupBy(o => o.Stage)
                .Select(group => new OpportunityStageSummary
                {
                    Stage = group.Key,
                    Count = group.Count(),
                    Amount = group.Sum(o => o.Amount)
                })
                .ToListAsync();

            var activeStages = new[]
            {
                OpportunityStage.Qualification,
                OpportunityStage.Proposal,
                OpportunityStage.Negotiation
            };
            var query = visibleOpportunities.Include(o => o.Lead).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(o => o.Title.Contains(term) ||
                    (o.Lead != null && (o.Lead.Name.Contains(term) || o.Lead.Company.Contains(term))));
            }
            if (stage.HasValue)
                query = query.Where(o => o.Stage == stage.Value);

            var summariesByStage = groupedStages.ToDictionary(item => item.Stage);
            var stageSummaries = Enum.GetValues<OpportunityStage>()
                .Select(value => summariesByStage.TryGetValue(value, out var summary)
                    ? summary
                    : new OpportunityStageSummary { Stage = value })
                .ToList();

            var model = new OpportunityListViewModel
            {
                Opportunities = await query.OrderBy(o => o.ExpectedCloseDate).ToListAsync(),
                StageSummaries = stageSummaries,
                Search = search,
                Stage = stage,
                TotalPipelineValue = stageSummaries.Sum(item => item.Amount),
                WonRevenue = stageSummaries.Where(item => item.Stage == OpportunityStage.Won).Sum(item => item.Amount),
                OpenOpportunityValue = stageSummaries.Where(item => activeStages.Contains(item.Stage)).Sum(item => item.Amount)
            };
            return View(model);
        }

        [HttpGet]
        [CrmPermission("Opportunities", "Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateLeadsAsync();
            return View(new Opportunity { ExpectedCloseDate = DateTime.Today.AddMonths(1) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("Opportunities", "Create")]
        public async Task<IActionResult> Create(Opportunity opportunity)
        {
            ValidateStage(opportunity.Stage);
            if (!await (await _access.LeadsForAsync(User)).AnyAsync(l => l.Id == opportunity.LeadId))
                ModelState.AddModelError(nameof(opportunity.LeadId), "Select a lead you are allowed to access.");

            if (!ModelState.IsValid)
            {
                await PopulateLeadsAsync(opportunity.LeadId);
                return View(opportunity);
            }

            try
            {
                opportunity.CreatedAt = DateTime.UtcNow;
                _context.Opportunities.Add(opportunity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Opportunity created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unable to create opportunity for lead {LeadId}.", opportunity.LeadId);
                ModelState.AddModelError(string.Empty, "The opportunity could not be saved. Please try again.");
                await PopulateLeadsAsync(opportunity.LeadId);
                return View(opportunity);
            }
        }

        [HttpGet]
        [CrmPermission("Opportunities", "View")]
        public async Task<IActionResult> Details(int id)
        {
            var opportunity = await (await _access.OpportunitiesForAsync(User)).AsNoTracking()
                .Include(o => o.Lead).FirstOrDefaultAsync(o => o.Id == id);
            return opportunity == null ? NotFound() : View(opportunity);
        }

        [HttpGet]
        [CrmPermission("Opportunities", "Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var opportunity = await (await _access.OpportunitiesForAsync(User)).FirstOrDefaultAsync(o => o.Id == id);
            if (opportunity == null)
                return NotFound();
            await PopulateLeadsAsync(opportunity.LeadId);
            return View(opportunity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CrmPermission("Opportunities", "Edit")]
        public async Task<IActionResult> Edit(int id, Opportunity opportunity)
        {
            if (id != opportunity.Id)
                return BadRequest();
            ValidateStage(opportunity.Stage);
            if (!await (await _access.LeadsForAsync(User)).AnyAsync(l => l.Id == opportunity.LeadId))
                ModelState.AddModelError(nameof(opportunity.LeadId), "Select a lead you are allowed to access.");

            if (!ModelState.IsValid)
            {
                await PopulateLeadsAsync(opportunity.LeadId);
                return View(opportunity);
            }

            var existing = await (await _access.OpportunitiesForAsync(User)).FirstOrDefaultAsync(o => o.Id == id);
            if (existing == null)
                return NotFound();

            existing.LeadId = opportunity.LeadId;
            existing.Title = opportunity.Title;
            existing.Amount = opportunity.Amount;
            existing.Stage = opportunity.Stage;
            existing.Probability = opportunity.Probability;
            existing.ExpectedCloseDate = opportunity.ExpectedCloseDate;
            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Opportunity updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unable to update opportunity {OpportunityId}.", id);
                ModelState.AddModelError(string.Empty, "The opportunity could not be saved. Please try again.");
                await PopulateLeadsAsync(opportunity.LeadId);
                return View(opportunity);
            }
        }

        [HttpGet]
        [CrmPermission("Opportunities", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var opportunity = await (await _access.OpportunitiesForAsync(User)).AsNoTracking()
                .Include(o => o.Lead).FirstOrDefaultAsync(o => o.Id == id);
            return opportunity == null ? NotFound() : View(opportunity);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [CrmPermission("Opportunities", "Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var opportunity = await (await _access.OpportunitiesForAsync(User)).FirstOrDefaultAsync(o => o.Id == id);
            if (opportunity == null)
                return NotFound();
            try
            {
                _context.Opportunities.Remove(opportunity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Opportunity deleted successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Unable to delete opportunity {OpportunityId}.", id);
                TempData["Error"] = "The opportunity could not be deleted. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        private void ValidateStage(OpportunityStage stage)
        {
            if (!Enum.IsDefined(stage))
                ModelState.AddModelError(nameof(Opportunity.Stage), "Select a valid stage.");
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
