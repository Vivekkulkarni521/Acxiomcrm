using ACXIOMCRM.Models;

namespace ACXIOMCRM.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenOpportunities { get; set; }
        public int FollowUps { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalSalesPipelineValue { get; set; }
        public string CurrentUserRole { get; set; } = "No role assigned";
        public List<string> LeadStatusLabels { get; set; } = new();
        public List<int> LeadStatusValues { get; set; } = new();
        public List<string> OpportunityStageLabels { get; set; } = new();
        public List<int> OpportunityStageValues { get; set; } = new();
        public List<CrmActivity> UpcomingActivities { get; set; } = new();
        public List<CrmActivity> OverdueActivities { get; set; } = new();
    }
}
