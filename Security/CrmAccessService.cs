using ACXIOMCRM.Data;
using ACXIOMCRM.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ACXIOMCRM.Security
{
    public class CrmAccessService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CrmAccessService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IQueryable<Customer>> CustomersForAsync(ClaimsPrincipal principal)
        {
            var user = await GetActiveUserAsync(principal);
            if (principal.IsInRole("Admin"))
                return _context.Customers;
            if (user == null)
                return _context.Customers.Where(_ => false);
            if (principal.IsInRole("Manager") && user.TeamId.HasValue)
            {
                var userId = user.Id;
                var teamId = user.TeamId.Value;
                return _context.Customers.Where(c => c.AssignedToUserId == userId ||
                    (c.AssignedToUser != null && c.AssignedToUser.TeamId == teamId));
            }
            return _context.Customers.Where(c => c.AssignedToUserId == user.Id);
        }

        public async Task<IQueryable<Lead>> LeadsForAsync(ClaimsPrincipal principal)
        {
            var user = await GetActiveUserAsync(principal);
            if (principal.IsInRole("Admin"))
                return _context.Leads;
            if (user == null)
                return _context.Leads.Where(_ => false);
            if (principal.IsInRole("Manager") && user.TeamId.HasValue)
            {
                var userId = user.Id;
                var teamId = user.TeamId.Value;
                return _context.Leads.Where(l => l.AssignedToUserId == userId ||
                    (l.AssignedToUser != null && l.AssignedToUser.TeamId == teamId));
            }
            return _context.Leads.Where(l => l.AssignedToUserId == user.Id);
        }

        public async Task<IQueryable<FollowUp>> FollowUpsForAsync(ClaimsPrincipal principal)
        {
            var leadIds = (await LeadsForAsync(principal)).Select(l => l.Id);
            return _context.FollowUps.Where(f => leadIds.Contains(f.LeadId));
        }

        public async Task<IQueryable<Opportunity>> OpportunitiesForAsync(ClaimsPrincipal principal)
        {
            var leadIds = (await LeadsForAsync(principal)).Select(l => l.Id);
            return _context.Opportunities.Where(o => leadIds.Contains(o.LeadId));
        }

        public async Task<IQueryable<CrmActivity>> ActivitiesForAsync(ClaimsPrincipal principal)
        {
            if (principal.IsInRole("Admin"))
                return _context.Activities;

            var user = await GetActiveUserAsync(principal);
            if (user == null)
                return _context.Activities.Where(_ => false);

            var customerIds = (await CustomersForAsync(principal)).Select(c => c.Id);
            var leadIds = (await LeadsForAsync(principal)).Select(l => l.Id);
            var opportunityIds = (await OpportunitiesForAsync(principal)).Select(o => o.Id);
            return _context.Activities.Where(a =>
                (a.CustomerId == null || customerIds.Contains(a.CustomerId.Value)) &&
                (a.LeadId == null || leadIds.Contains(a.LeadId.Value)) &&
                (a.OpportunityId == null || opportunityIds.Contains(a.OpportunityId.Value)) &&
                (a.CustomerId != null || a.LeadId != null || a.OpportunityId != null));
        }

        public async Task<ApplicationUser?> GetActiveUserAsync(ClaimsPrincipal principal)
        {
            var user = await _userManager.GetUserAsync(principal);
            return user is { IsActive: true } ? user : null;
        }
    }
}
