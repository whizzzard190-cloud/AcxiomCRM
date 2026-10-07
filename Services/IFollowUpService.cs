using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface IFollowUpService
{
    Task<PagedList<FollowUp>> GetFollowUpsAsync(
        ClaimsPrincipal user,
        string? filter = null, // "upcoming", "overdue", "completed", "all"
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10);

    Task<FollowUp?> GetFollowUpByIdAsync(ClaimsPrincipal user, int id);
    Task<(bool Success, string? Error, FollowUp? FollowUp)> CreateFollowUpAsync(ClaimsPrincipal user, FollowUp followUp);
    Task<(bool Success, string? Error)> CompleteFollowUpAsync(ClaimsPrincipal user, int id, string? completionRemarks = null);
    Task<(bool Success, string? Error)> RescheduleFollowUpAsync(ClaimsPrincipal user, int id, DateTime newDate, string? reason = null);
    Task<(bool Success, string? Error)> DeleteFollowUpAsync(ClaimsPrincipal user, int id);
}
