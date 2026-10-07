using System.Security.Claims;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardMetricsAsync(
        ClaimsPrincipal user,
        string? dateFilter = "ThisMonth",
        DateTime? fromDate = null,
        DateTime? toDate = null);

    Task<ChartDataResponse> GetChartDataAsync(
        ClaimsPrincipal user,
        string? dateFilter = "ThisMonth",
        DateTime? fromDate = null,
        DateTime? toDate = null);
}
