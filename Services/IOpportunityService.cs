using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services;

public interface IOpportunityService
{
    Task<PagedList<Opportunity>> GetOpportunitiesAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? stage = null,
        string? status = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10);

    Task<List<Opportunity>> GetPipelineOpportunitiesAsync(ClaimsPrincipal user);
    Task<Opportunity?> GetOpportunityByIdAsync(ClaimsPrincipal user, int id);
    Task<(bool Success, string? Error, Opportunity? Opportunity)> CreateOpportunityAsync(ClaimsPrincipal user, Opportunity opportunity);
    Task<(bool Success, string? Error, Opportunity? Opportunity)> UpdateOpportunityAsync(ClaimsPrincipal user, Opportunity opportunity);
    Task<(bool Success, string? Error)> ChangeStageAsync(ClaimsPrincipal user, int id, string newStage);
    Task<(bool Success, string? Error)> DeleteOpportunityAsync(ClaimsPrincipal user, int id);
    bool IsValidStageTransition(string currentStage, string newStage);
}
