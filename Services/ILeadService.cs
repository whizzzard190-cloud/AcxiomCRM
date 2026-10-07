using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface ILeadService
{
    Task<PagedList<Lead>> GetLeadsAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? priority = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10);

    Task<Lead?> GetLeadByIdAsync(ClaimsPrincipal user, int id);
    Task<(bool Success, string? Error, Lead? Lead)> CreateLeadAsync(ClaimsPrincipal user, Lead lead);
    Task<(bool Success, string? Error, Lead? Lead)> UpdateLeadAsync(ClaimsPrincipal user, Lead lead);
    Task<(bool Success, string? Error)> ChangeStatusAsync(ClaimsPrincipal user, int leadId, string newStatus);
    Task<(bool Success, string? Error, Customer? Customer, Opportunity? Opportunity)> ConvertLeadAsync(ClaimsPrincipal user, ConvertLeadViewModel model);
    Task<(bool Success, string? Error)> DeleteLeadAsync(ClaimsPrincipal user, int id);
    bool IsValidStatus(string status);
    bool IsValidTransition(string currentStatus, string nextStatus);
}
