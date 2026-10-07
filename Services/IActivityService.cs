using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface IActivityService
{
    Task<PagedList<Activity>> GetActivitiesAsync(
        ClaimsPrincipal user,
        string? type = null,
        string? status = null,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10);

    Task<Activity?> GetActivityByIdAsync(ClaimsPrincipal user, int id);
    Task<(bool Success, string? Error, Activity? Activity)> CreateActivityAsync(ClaimsPrincipal user, Activity activity);
    Task<(bool Success, string? Error, Activity? Activity)> UpdateActivityAsync(ClaimsPrincipal user, Activity activity);
    Task<(bool Success, string? Error)> DeleteActivityAsync(ClaimsPrincipal user, int id);
}
