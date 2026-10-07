using System.Security.Claims;
using System.Text;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface IReportService
{
    Task<(List<CustomerReportItem> Items, byte[] CsvBytes)> GetCustomerReportAsync(ClaimsPrincipal user, string? search, string? status, DateTime? fromDate, DateTime? toDate);
    Task<(List<LeadReportItem> Items, byte[] CsvBytes)> GetLeadReportAsync(ClaimsPrincipal user, string? status, string? priority, DateTime? fromDate, DateTime? toDate);
    Task<(List<FollowUpReportItem> Items, byte[] CsvBytes)> GetFollowUpReportAsync(ClaimsPrincipal user, string? status, string? type, DateTime? fromDate, DateTime? toDate);
    Task<(List<OpportunityReportItem> Items, byte[] CsvBytes)> GetOpportunityReportAsync(ClaimsPrincipal user, string? stage, string? status, DateTime? fromDate, DateTime? toDate);
    Task<(List<PipelineReportItem> StageWise, List<PipelineOwnerReportItem> OwnerWise, byte[] CsvBytes)> GetPipelineReportAsync(ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate);
    Task<(List<ConversionReportItem> Items, byte[] CsvBytes)> GetConversionReportAsync(ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate);
    Task<(List<UserActivityReportItem> Items, byte[] CsvBytes)> GetUserActivityReportAsync(ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate);
    Task<(List<AuditReportItem> Items, byte[] CsvBytes)> GetAuditReportAsync(ClaimsPrincipal user, string? module, DateTime? fromDate, DateTime? toDate);
}

public class CustomerReportItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public class LeadReportItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal ExpectedValue { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public class FollowUpReportItem
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssociatedWith { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
}

public class OpportunityReportItem
{
    public string Name { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal WeightedValue => Amount * Probability / 100m;
    public string Status { get; set; } = string.Empty;
    public DateTime ExpectedCloseDate { get; set; }
    public string Owner { get; set; } = string.Empty;
}

public class PipelineReportItem
{
    public string Stage { get; set; } = string.Empty;
    public int DealCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class PipelineOwnerReportItem
{
    public string Owner { get; set; } = string.Empty;
    public int DealCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class ConversionReportItem
{
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? OpportunityName { get; set; }
    public decimal? OpportunityAmount { get; set; }
    public DateTime ConvertedDate { get; set; }
    public string ConvertedBy { get; set; } = string.Empty;
}

public class UserActivityReportItem
{
    public string UserName { get; set; } = string.Empty;
    public int Calls { get; set; }
    public int Meetings { get; set; }
    public int Emails { get; set; }
    public int Tasks { get; set; }
    public int TotalActivities => Calls + Meetings + Emails + Tasks;
}

public class AuditReportItem
{
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? RecordId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
