using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    // Date Filters
    public string DateFilter { get; set; } = "ThisMonth"; // Today, ThisWeek, ThisMonth, Custom
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    // Core KPI Cards
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }

    // Role-specific extras
    public AdminStats? AdminExtras { get; set; }
    public List<ManagerTeamStat>? ManagerExtras { get; set; }
    public List<FollowUp>? UpcomingFollowUps { get; set; }
}

public class AdminStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedOutUsers { get; set; }
    public List<AuditLog> RecentSecurityEvents { get; set; } = new();
}

public class ManagerTeamStat
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int CustomerCount { get; set; }
    public int LeadCount { get; set; }
    public int OpenOppCount { get; set; }
    public decimal PipelineValue { get; set; }
}

public class ChartDataResponse
{
    public ChartDataset LeadStatus { get; set; } = new();
    public ChartDataset OpportunityPipeline { get; set; } = new();
    public ChartDataset MonthlySales { get; set; } = new();
}

public class ChartDataset
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
}
