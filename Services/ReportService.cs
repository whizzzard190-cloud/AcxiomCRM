using System.Security.Claims;
using System.Text;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;

    public ReportService(ApplicationDbContext context, IResourceAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<(List<CustomerReportItem> Items, byte[] CsvBytes)> GetCustomerReportAsync(
        ClaimsPrincipal user, string? search, string? status, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterCustomersAsync(user, _context.Customers.Include(c => c.Owner).AsNoTracking());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.CustomerName.Contains(term) || (c.CompanyName != null && c.CompanyName.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }
        if (fromDate.HasValue) query = query.Where(c => c.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(c => c.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var items = await query.OrderByDescending(c => c.CreatedDate).Select(c => new CustomerReportItem
        {
            Code = c.CustomerCode,
            Name = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            Company = c.CompanyName,
            Status = c.Status,
            Owner = c.Owner != null ? c.Owner.FullName : "Unassigned",
            CreatedDate = c.CreatedDate
        }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Customer Code,Customer Name,Email,Phone,Company,Status,Owner,Created Date");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{Escape(i.Code)}\",\"{Escape(i.Name)}\",\"{Escape(i.Email)}\",\"{Escape(i.Phone)}\",\"{Escape(i.Company)}\",\"{Escape(i.Status)}\",\"{Escape(i.Owner)}\",\"{i.CreatedDate:yyyy-MM-dd HH:mm}\"");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<LeadReportItem> Items, byte[] CsvBytes)> GetLeadReportAsync(
        ClaimsPrincipal user, string? status, string? priority, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterLeadsAsync(user, _context.Leads.Include(l => l.AssignedUser).AsNoTracking());

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(l => l.Priority == priority);
        if (fromDate.HasValue) query = query.Where(l => l.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(l => l.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var items = await query.OrderByDescending(l => l.CreatedDate).Select(l => new LeadReportItem
        {
            Code = l.LeadCode,
            Name = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            Company = l.CompanyName,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            AssignedTo = l.AssignedUser != null ? l.AssignedUser.FullName : "Unassigned",
            CreatedDate = l.CreatedDate
        }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Lead Code,Lead Name,Email,Phone,Company,Status,Priority,Expected Value,Assigned To,Created Date");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{Escape(i.Code)}\",\"{Escape(i.Name)}\",\"{Escape(i.Email)}\",\"{Escape(i.Phone)}\",\"{Escape(i.Company)}\",\"{Escape(i.Status)}\",\"{Escape(i.Priority)}\",{i.ExpectedValue},\"{Escape(i.AssignedTo)}\",\"{i.CreatedDate:yyyy-MM-dd HH:mm}\"");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<FollowUpReportItem> Items, byte[] CsvBytes)> GetFollowUpReportAsync(
        ClaimsPrincipal user, string? status, string? type, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterFollowUpsAsync(user, _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedUser)
            .AsNoTracking());

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(f => f.FollowUpType == type);
        if (fromDate.HasValue) query = query.Where(f => f.FollowUpDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(f => f.FollowUpDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var rawItems = await query.OrderByDescending(f => f.FollowUpDate).ToListAsync();
        var items = rawItems.Select(f => new FollowUpReportItem
        {
            Date = f.FollowUpDate,
            Type = f.FollowUpType,
            Subject = f.Subject,
            Status = f.IsOverdue ? "Overdue" : f.Status,
            AssociatedWith = f.Customer?.CustomerName ?? f.Lead?.LeadName ?? f.Opportunity?.OpportunityName ?? "-",
            AssignedTo = f.AssignedUser?.FullName ?? "Unassigned"
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Date & Time,Type,Subject,Status,Associated With,Assigned To");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{i.Date:yyyy-MM-dd HH:mm}\",\"{Escape(i.Type)}\",\"{Escape(i.Subject)}\",\"{Escape(i.Status)}\",\"{Escape(i.AssociatedWith)}\",\"{Escape(i.AssignedTo)}\"");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<OpportunityReportItem> Items, byte[] CsvBytes)> GetOpportunityReportAsync(
        ClaimsPrincipal user, string? stage, string? status, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterOpportunitiesAsync(user, _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .AsNoTracking());

        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
        if (fromDate.HasValue) query = query.Where(o => o.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(o => o.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var items = await query.OrderByDescending(o => o.CreatedDate).Select(o => new OpportunityReportItem
        {
            Name = o.OpportunityName,
            Customer = o.Customer != null ? o.Customer.CustomerName : "N/A",
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            Status = o.Status,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Owner = o.AssignedUser != null ? o.AssignedUser.FullName : "Unassigned"
        }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Opportunity Name,Customer,Amount,Stage,Probability,Weighted Value,Status,Expected Close Date,Owner");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{Escape(i.Name)}\",\"{Escape(i.Customer)}\",{i.Amount},\"{Escape(i.Stage)}\",{i.Probability},{i.WeightedValue},\"{Escape(i.Status)}\",\"{i.ExpectedCloseDate:yyyy-MM-dd}\",\"{Escape(i.Owner)}\"");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<PipelineReportItem> StageWise, List<PipelineOwnerReportItem> OwnerWise, byte[] CsvBytes)> GetPipelineReportAsync(
        ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterOpportunitiesAsync(user, _context.Opportunities
            .Include(o => o.AssignedUser)
            .AsNoTracking());

        if (fromDate.HasValue) query = query.Where(o => o.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(o => o.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var allOpps = await query.ToListAsync();

        var stageWise = allOpps.GroupBy(o => o.Stage)
            .Select(g => new PipelineReportItem
            {
                Stage = g.Key,
                DealCount = g.Count(),
                TotalAmount = g.Sum(o => o.Amount),
                WeightedAmount = g.Sum(o => o.WeightedAmount)
            }).ToList();

        var ownerWise = allOpps.GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
            .Select(g => new PipelineOwnerReportItem
            {
                Owner = g.Key,
                DealCount = g.Count(),
                TotalAmount = g.Sum(o => o.Amount),
                WeightedAmount = g.Sum(o => o.WeightedAmount)
            }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("=== STAGE-WISE PIPELINE ===");
        sb.AppendLine("Stage,Deal Count,Total Amount,Weighted Amount");
        foreach (var s in stageWise)
        {
            sb.AppendLine($"\"{Escape(s.Stage)}\",{s.DealCount},{s.TotalAmount},{s.WeightedAmount}");
        }

        sb.AppendLine();
        sb.AppendLine("=== OWNER-WISE PIPELINE ===");
        sb.AppendLine("Owner,Deal Count,Total Amount,Weighted Amount");
        foreach (var o in ownerWise)
        {
            sb.AppendLine($"\"{Escape(o.Owner)}\",{o.DealCount},{o.TotalAmount},{o.WeightedAmount}");
        }

        return (stageWise, ownerWise, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<ConversionReportItem> Items, byte[] CsvBytes)> GetConversionReportAsync(
        ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterLeadsAsync(user, _context.Leads
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.ConvertedOpportunity)
            .Include(l => l.AssignedUser)
            .Where(l => l.Status == "Converted")
            .AsNoTracking());

        if (fromDate.HasValue) query = query.Where(l => l.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(l => l.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var items = await query.OrderByDescending(l => l.CreatedDate).Select(l => new ConversionReportItem
        {
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            CustomerName = l.ConvertedCustomer != null ? l.ConvertedCustomer.CustomerName : "N/A",
            OpportunityName = l.ConvertedOpportunity != null ? l.ConvertedOpportunity.OpportunityName : null,
            OpportunityAmount = l.ConvertedOpportunity != null ? l.ConvertedOpportunity.Amount : null,
            ConvertedDate = l.CreatedDate,
            ConvertedBy = l.AssignedUser != null ? l.AssignedUser.FullName : "System"
        }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Lead Code,Lead Name,Customer Name,Opportunity Name,Opportunity Amount,Converted Date,Converted By");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{Escape(i.LeadCode)}\",\"{Escape(i.LeadName)}\",\"{Escape(i.CustomerName)}\",\"{Escape(i.OpportunityName)}\",{i.OpportunityAmount?.ToString() ?? "0"},\"{i.ConvertedDate:yyyy-MM-dd}\",\"{Escape(i.ConvertedBy)}\"");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<UserActivityReportItem> Items, byte[] CsvBytes)> GetUserActivityReportAsync(
        ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate)
    {
        var query = await _authService.FilterActivitiesAsync(user, _context.Activities
            .Include(a => a.AssignedUser)
            .AsNoTracking());

        if (fromDate.HasValue) query = query.Where(a => a.ActivityDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.ActivityDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var activities = await query.ToListAsync();

        var items = activities.GroupBy(a => a.AssignedUser?.FullName ?? "Unassigned")
            .Select(g => new UserActivityReportItem
            {
                UserName = g.Key,
                Calls = g.Count(a => a.ActivityType == "Call"),
                Meetings = g.Count(a => a.ActivityType == "Meeting"),
                Emails = g.Count(a => a.ActivityType == "Email"),
                Tasks = g.Count(a => a.ActivityType == "Task")
            }).OrderByDescending(u => u.TotalActivities).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("User Name,Calls,Meetings,Emails,Tasks,Total Activities");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{Escape(i.UserName)}\",{i.Calls},{i.Meetings},{i.Emails},{i.Tasks},{i.TotalActivities}");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<(List<AuditReportItem> Items, byte[] CsvBytes)> GetAuditReportAsync(
        ClaimsPrincipal user, string? module, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.AuditLogs.Include(a => a.User).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(a => a.Module == module);
        if (fromDate.HasValue) query = query.Where(a => a.CreatedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        var items = await query.OrderByDescending(a => a.CreatedDate).Select(a => new AuditReportItem
        {
            Timestamp = a.CreatedDate,
            User = a.User != null ? a.User.FullName : (a.UserId ?? "System"),
            Module = a.Module,
            Action = a.Action,
            RecordId = a.RecordId,
            Result = a.Result,
            IpAddress = a.IpAddress
        }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp (UTC),User,Module,Action,Record ID,Result,IP Address");
        foreach (var i in items)
        {
            sb.AppendLine($"\"{i.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{Escape(i.User)}\",\"{Escape(i.Module)}\",\"{Escape(i.Action)}\",\"{Escape(i.RecordId)}\",\"{Escape(i.Result)}\",\"{Escape(i.IpAddress)}\"");
        }

        return (items, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\"", "\"\"");
    }
}
