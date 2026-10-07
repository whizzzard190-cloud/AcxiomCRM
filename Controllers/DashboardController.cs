using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? dateFilter = "ThisMonth", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var model = await _dashboardService.GetDashboardMetricsAsync(User, dateFilter, fromDate, toDate);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ChartData(string? dateFilter = "ThisMonth", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var chartData = await _dashboardService.GetChartDataAsync(User, dateFilter, fromDate, toDate);
        return Json(chartData);
    }
}
