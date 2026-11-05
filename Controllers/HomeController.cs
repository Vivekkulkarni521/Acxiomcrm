using System.Diagnostics;
using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using ACXIOMCRM.Models.ViewModels;
using ACXIOMCRM.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ACXIOMCRM.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CrmAccessService _access;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ApplicationDbContext context, CrmAccessService access, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _access = access;
        _userManager = userManager;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var customers = await _access.CustomersForAsync(User);
        var leads = await _access.LeadsForAsync(User);
        var followUps = await _access.FollowUpsForAsync(User);
        var opportunities = await _access.OpportunitiesForAsync(User);
        var activities = await _access.ActivitiesForAsync(User);
        var currentUser = await _userManager.GetUserAsync(User);
        var roles = currentUser == null ? Array.Empty<string>() : await _userManager.GetRolesAsync(currentUser);
        var now = DateTime.Now;

        var leadStatusData = await leads.AsNoTracking()
            .GroupBy(lead => lead.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(item => item.Status)
            .ToListAsync();

        var opportunityStageData = await opportunities.AsNoTracking()
            .GroupBy(opportunity => opportunity.Stage)
            .Select(group => new
            {
                Stage = group.Key,
                Count = group.Count(),
                Amount = group.Sum(opportunity => opportunity.Amount)
            })
            .ToListAsync();

        var activeStages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation
        };
        var activeOpportunityData = opportunityStageData
            .Where(item => activeStages.Contains(item.Stage))
            .ToList();

        var stageLabels = Enum.GetValues<OpportunityStage>();
        var model = new DashboardViewModel
        {
            TotalCustomers = await customers.CountAsync(),
            TotalLeads = leadStatusData.Sum(item => item.Count),
            OpenOpportunities = activeOpportunityData.Sum(item => item.Count),
            FollowUps = await followUps.CountAsync(),
            WonOpportunities = opportunityStageData
                .Where(item => item.Stage == OpportunityStage.Won)
                .Sum(item => item.Count),
            LostOpportunities = opportunityStageData
                .Where(item => item.Stage == OpportunityStage.Lost)
                .Sum(item => item.Count),
            TotalSalesPipelineValue = activeOpportunityData.Sum(item => item.Amount),
            CurrentUserRole = roles.Count == 0 ? "No role assigned" : string.Join(", ", roles),
            LeadStatusLabels = leadStatusData.Select(item => item.Status).ToList(),
            LeadStatusValues = leadStatusData.Select(item => item.Count).ToList(),
            OpportunityStageLabels = stageLabels.Select(stage => stage.ToString()).ToList(),
            OpportunityStageValues = stageLabels.Select(stage => opportunityStageData
                .Where(item => item.Stage == stage)
                .Sum(item => item.Count)).ToList(),
            UpcomingActivities = await activities.AsNoTracking()
                .Where(a => a.Status == ActivityStatus.Planned && a.ScheduledAt >= now)
                .Include(a => a.Customer).Include(a => a.Lead).Include(a => a.Opportunity)
                .OrderBy(a => a.ScheduledAt).Take(5).ToListAsync(),
            OverdueActivities = await activities.AsNoTracking()
                .Where(a => a.Status == ActivityStatus.Overdue ||
                    (a.Status == ActivityStatus.Planned && a.ScheduledAt < now))
                .Include(a => a.Customer).Include(a => a.Lead).Include(a => a.Opportunity)
                .OrderBy(a => a.ScheduledAt).Take(5).ToListAsync()
        };

        return View(model);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
