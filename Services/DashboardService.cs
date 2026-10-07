using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardService(
        ApplicationDbContext context,
        IResourceAuthorizationService authService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _authService = authService;
        _userManager = userManager;
    }

    public async Task<DashboardViewModel> GetDashboardMetricsAsync(
        ClaimsPrincipal user,
        string? dateFilter = "ThisMonth",
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var (start, end) = ResolveDateRange(dateFilter, fromDate, toDate);

        // Filtered Queries according to Role Scope
        var custQuery = await _authService.FilterCustomersAsync(user, _context.Customers.AsNoTracking());
        var leadQuery = await _authService.FilterLeadsAsync(user, _context.Leads.AsNoTracking());
        var oppQuery = await _authService.FilterOpportunitiesAsync(user, _context.Opportunities.AsNoTracking());

        if (start.HasValue)
        {
            custQuery = custQuery.Where(c => c.CreatedDate >= start.Value);
            leadQuery = leadQuery.Where(l => l.CreatedDate >= start.Value);
            oppQuery = oppQuery.Where(o => o.CreatedDate >= start.Value);
        }
        if (end.HasValue)
        {
            custQuery = custQuery.Where(c => c.CreatedDate <= end.Value);
            leadQuery = leadQuery.Where(l => l.CreatedDate <= end.Value);
            oppQuery = oppQuery.Where(o => o.CreatedDate <= end.Value);
        }

        // KPIs
        int totalCustomers = await custQuery.CountAsync();
        int totalLeads = await leadQuery.CountAsync();
        int openLeads = await leadQuery.Where(l => l.Status == "New" || l.Status == "Contacted" || l.Status == "Qualified" || l.Status == "Unqualified").CountAsync();

        int totalOpps = await oppQuery.CountAsync();
        int openOpps = await oppQuery.Where(o => o.Status == "Open").CountAsync();
        int wonOpps = await oppQuery.Where(o => o.Status == "Won").CountAsync();
        int lostOpps = await oppQuery.Where(o => o.Status == "Lost").CountAsync();

        decimal pipelineValue = await oppQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0m;
        decimal weightedValue = await oppQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

        var vm = new DashboardViewModel
        {
            DateFilter = dateFilter ?? "ThisMonth",
            FromDate = fromDate,
            ToDate = toDate,
            TotalCustomers = totalCustomers,
            TotalLeads = totalLeads,
            OpenLeads = openLeads,
            TotalOpportunities = totalOpps,
            OpenOpportunities = openOpps,
            WonOpportunities = wonOpps,
            LostOpportunities = lostOpps,
            TotalPipelineValue = pipelineValue,
            WeightedPipelineValue = weightedValue
        };

        // Role Extras
        if (user.IsInRole("Admin"))
        {
            int totalUsers = await _context.Users.CountAsync();
            int activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            int lockedUsers = await _context.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);

            var recentSecurityLogs = await _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.Module == "Auth" || a.Module == "User")
                .OrderByDescending(a => a.CreatedDate)
                .Take(5)
                .ToListAsync();

            vm.AdminExtras = new AdminStats
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                LockedOutUsers = lockedUsers,
                RecentSecurityEvents = recentSecurityLogs
            };
        }
        else if (user.IsInRole("Manager"))
        {
            var accessibleUserIds = await _authService.GetAccessibleUserIdsAsync(user);
            var teamMembers = await _context.Users
                .Where(u => accessibleUserIds.Contains(u.Id))
                .ToListAsync();

            var teamStats = new List<ManagerTeamStat>();
            foreach (var m in teamMembers)
            {
                var custCount = await _context.Customers.CountAsync(c => c.OwnerId == m.Id);
                var leadCount = await _context.Leads.CountAsync(l => l.AssignedTo == m.Id);
                var openCount = await _context.Opportunities.CountAsync(o => o.AssignedTo == m.Id && o.Status == "Open");
                var pipeVal = await _context.Opportunities.Where(o => o.AssignedTo == m.Id && o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0m;

                teamStats.Add(new ManagerTeamStat
                {
                    UserId = m.Id,
                    UserName = m.FullName,
                    CustomerCount = custCount,
                    LeadCount = leadCount,
                    OpenOppCount = openCount,
                    PipelineValue = pipeVal
                });
            }
            vm.ManagerExtras = teamStats;
        }
        else
        {
            // Sales Executive: Upcoming follow-ups
            var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var followUps = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Where(f => f.AssignedTo == currentUserId && f.Status == "Planned")
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            vm.UpcomingFollowUps = followUps;
        }

        return vm;
    }

    public async Task<ChartDataResponse> GetChartDataAsync(
        ClaimsPrincipal user,
        string? dateFilter = "ThisMonth",
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var (start, end) = ResolveDateRange(dateFilter, fromDate, toDate);

        var leadQuery = await _authService.FilterLeadsAsync(user, _context.Leads.AsNoTracking());
        var oppQuery = await _authService.FilterOpportunitiesAsync(user, _context.Opportunities.AsNoTracking());

        if (start.HasValue)
        {
            leadQuery = leadQuery.Where(l => l.CreatedDate >= start.Value);
            oppQuery = oppQuery.Where(o => o.CreatedDate >= start.Value);
        }
        if (end.HasValue)
        {
            leadQuery = leadQuery.Where(l => l.CreatedDate <= end.Value);
            oppQuery = oppQuery.Where(o => o.CreatedDate <= end.Value);
        }

        // 1. Lead Status Chart (New, Contacted, Qualified, Unqualified, Lost, Converted)
        string[] leadStatuses = ["New", "Contacted", "Qualified", "Unqualified", "Lost", "Converted"];
        var leadStatusCounts = await leadQuery
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var leadChart = new ChartDataset
        {
            Labels = leadStatuses.ToList(),
            Values = leadStatuses.Select(s => (decimal)(leadStatusCounts.FirstOrDefault(x => string.Equals(x.Status, s, StringComparison.OrdinalIgnoreCase))?.Count ?? 0)).ToList()
        };

        // 2. Opportunity Pipeline Chart (Qualification, Proposal, Negotiation, Won, Lost)
        string[] oppStages = ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];
        var oppStageSums = await oppQuery
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Total = g.Sum(o => o.Amount) })
            .ToListAsync();

        var oppChart = new ChartDataset
        {
            Labels = oppStages.ToList(),
            Values = oppStages.Select(s => oppStageSums.FirstOrDefault(x => string.Equals(x.Stage, s, StringComparison.OrdinalIgnoreCase))?.Total ?? 0m).ToList()
        };

        // 3. Monthly Sales (Won totals per month for past 6 months)
        var months = new List<string>();
        var wonTotals = new List<decimal>();

        var allWonOpps = await (await _authService.FilterOpportunitiesAsync(user, _context.Opportunities.AsNoTracking()))
            .Where(o => o.Status == "Won")
            .ToListAsync();

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = DateTime.UtcNow.AddMonths(-i);
            var label = targetMonth.ToString("MMM yyyy");
            months.Add(label);

            var totalWon = allWonOpps
                .Where(o => o.ExpectedCloseDate.Year == targetMonth.Year && o.ExpectedCloseDate.Month == targetMonth.Month)
                .Sum(o => o.Amount);

            wonTotals.Add(totalWon);
        }

        var salesChart = new ChartDataset
        {
            Labels = months,
            Values = wonTotals
        };

        return new ChartDataResponse
        {
            LeadStatus = leadChart,
            OpportunityPipeline = oppChart,
            MonthlySales = salesChart
        };
    }

    private static (DateTime? Start, DateTime? End) ResolveDateRange(string? dateFilter, DateTime? fromDate, DateTime? toDate)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        return (dateFilter?.ToLower()) switch
        {
            "today" => (today, today.AddDays(1).AddTicks(-1)),
            "thisweek" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(1).AddTicks(-1)),
            "thismonth" => (new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1)),
            "custom" when fromDate.HasValue => (fromDate.Value.Date, toDate?.Date.AddDays(1).AddTicks(-1) ?? today.AddDays(1).AddTicks(-1)),
            _ => (null, null)
        };
    }
}
