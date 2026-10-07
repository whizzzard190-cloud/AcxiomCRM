using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CustomerReport(string? search, string? status, DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetCustomerReportAsync(User, search, status, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"Customer_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> LeadReport(string? status, string? priority, DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetLeadReportAsync(User, status, priority, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"Lead_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> FollowUpReport(string? status, string? type, DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetFollowUpReportAsync(User, status, type, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"FollowUp_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> OpportunityReport(string? stage, string? status, DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetOpportunityReportAsync(User, stage, status, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"Opportunity_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.Stage = stage;
        ViewBag.Status = status;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> PipelineReport(DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (stageWise, ownerWise, csvBytes) = await _reportService.GetPipelineReportAsync(User, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"Pipeline_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.OwnerWise = ownerWise;
        return View(stageWise);
    }

    [HttpGet]
    public async Task<IActionResult> ConversionReport(DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetConversionReportAsync(User, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"Conversion_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UserActivityReport(DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetUserActivityReportAsync(User, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"User_Activity_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AuditReport(string? module, DateTime? fromDate, DateTime? toDate, bool export = false)
    {
        var (items, csvBytes) = await _reportService.GetAuditReportAsync(User, module, fromDate, toDate);
        if (export)
        {
            return File(csvBytes, "text/csv", $"Compliance_Audit_Report_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }
        ViewBag.Module = module;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        return View(items);
    }
}
