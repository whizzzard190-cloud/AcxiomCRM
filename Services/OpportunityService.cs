using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class OpportunityService : IOpportunityService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;

    private static readonly Dictionary<string, HashSet<string>> AllowedStageTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Qualification", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Proposal", "Lost" } },
        { "Proposal", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Negotiation", "Lost" } },
        { "Negotiation", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Won", "Lost" } },
        { "Won", new HashSet<string>(StringComparer.OrdinalIgnoreCase) }, // Terminal
        { "Lost", new HashSet<string>(StringComparer.OrdinalIgnoreCase) }  // Terminal
    };

    public OpportunityService(
        ApplicationDbContext context,
        IResourceAuthorizationService authService,
        IAuditService auditService)
    {
        _context = context;
        _authService = authService;
        _auditService = auditService;
    }

    public bool IsValidStageTransition(string currentStage, string newStage)
    {
        if (string.Equals(currentStage, newStage, StringComparison.OrdinalIgnoreCase))
            return true;

        if (AllowedStageTransitions.TryGetValue(currentStage, out var allowedNext))
        {
            return allowedNext.Contains(newStage);
        }

        return false;
    }

    public async Task<PagedList<Opportunity>> GetOpportunitiesAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? stage = null,
        string? status = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10)
    {
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .AsNoTracking();

        query = await _authService.FilterOpportunitiesAsync(user, query);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.OpportunityName.Contains(term) ||
                (o.Customer != null && o.Customer.CustomerName.Contains(term)) ||
                (o.Lead != null && o.Lead.LeadName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        bool isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLower()) switch
        {
            "name" => isDescending ? query.OrderByDescending(o => o.OpportunityName) : query.OrderBy(o => o.OpportunityName),
            "customer" => isDescending ? query.OrderByDescending(o => o.Customer != null ? o.Customer.CustomerName : "") : query.OrderBy(o => o.Customer != null ? o.Customer.CustomerName : ""),
            "amount" => isDescending ? query.OrderByDescending(o => o.Amount) : query.OrderBy(o => o.Amount),
            "stage" => isDescending ? query.OrderByDescending(o => o.Stage) : query.OrderBy(o => o.Stage),
            "probability" => isDescending ? query.OrderByDescending(o => o.Probability) : query.OrderBy(o => o.Probability),
            "closedate" => isDescending ? query.OrderByDescending(o => o.ExpectedCloseDate) : query.OrderBy(o => o.ExpectedCloseDate),
            "status" => isDescending ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            _ => isDescending ? query.OrderBy(o => o.CreatedDate) : query.OrderByDescending(o => o.CreatedDate)
        };

        int totalRecords = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedList<Opportunity>
        {
            Items = items,
            Pager = new PagerModel
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                SearchTerm = search,
                SortBy = sortBy,
                SortOrder = sortOrder,
                Controller = "Opportunities",
                Action = "Index"
            }
        };
    }

    public async Task<List<Opportunity>> GetPipelineOpportunitiesAsync(ClaimsPrincipal user)
    {
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .AsNoTracking();

        query = await _authService.FilterOpportunitiesAsync(user, query);
        return await query.ToListAsync();
    }

    public async Task<Opportunity?> GetOpportunityByIdAsync(ClaimsPrincipal user, int id)
    {
        var opp = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .Include(o => o.FollowUps)
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opp == null) return null;

        if (!await _authService.CanAccessOpportunityAsync(user, opp))
        {
            return null; // Deny access
        }

        return opp;
    }

    public async Task<(bool Success, string? Error, Opportunity? Opportunity)> CreateOpportunityAsync(ClaimsPrincipal user, Opportunity opportunity)
    {
        if (string.IsNullOrWhiteSpace(opportunity.OpportunityName))
            return (false, "Opportunity name is required.", null);

        var customer = await _context.Customers.FindAsync(opportunity.CustomerId);
        if (customer == null)
            return (false, "Specified customer was not found.", null);

        // Validate Amount: Amount cannot be negative; active opportunity needs Amount > 0.
        if (opportunity.Amount <= 0)
            return (false, "Opportunity Amount must be greater than 0.", null);

        // Validate Probability: 0 to 100 inclusive
        if (opportunity.Probability < 0 || opportunity.Probability > 100)
            return (false, "Probability must be between 0 and 100.", null);

        // Validate ExpectedCloseDate: cannot be in the past for active opportunity
        bool isActive = opportunity.Stage is "Qualification" or "Proposal" or "Negotiation";
        if (isActive && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            return (false, "Expected Close Date cannot be in the past.", null);

        opportunity.Status = opportunity.Stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!user.IsInRole("Admin") && !user.IsInRole("Manager"))
        {
            opportunity.AssignedTo = currentUserId;
        }
        else if (string.IsNullOrWhiteSpace(opportunity.AssignedTo))
        {
            opportunity.AssignedTo = currentUserId;
        }

        opportunity.CreatedDate = DateTime.UtcNow;

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Create",
            module: "Opportunity",
            entityName: "Opportunity",
            recordId: opportunity.OpportunityId.ToString(),
            oldValue: null,
            newValue: new
            {
                opportunity.OpportunityId,
                opportunity.OpportunityName,
                opportunity.CustomerId,
                opportunity.Amount,
                opportunity.Stage,
                opportunity.Probability,
                opportunity.Status,
                opportunity.AssignedTo
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, opportunity);
    }

    public async Task<(bool Success, string? Error, Opportunity? Opportunity)> UpdateOpportunityAsync(ClaimsPrincipal user, Opportunity opportunity)
    {
        var existing = await _context.Opportunities.FindAsync(opportunity.OpportunityId);
        if (existing == null)
            return (false, "Requested record was not found.", null);

        if (!await _authService.CanAccessOpportunityAsync(user, existing))
            return (false, "You are not authorized to perform this action.", null);

        if (string.IsNullOrWhiteSpace(opportunity.OpportunityName))
            return (false, "Opportunity name is required.", null);

        if (opportunity.Amount <= 0)
            return (false, "Opportunity Amount must be greater than 0.", null);

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
            return (false, "Probability must be between 0 and 100.", null);

        bool isActive = opportunity.Stage is "Qualification" or "Proposal" or "Negotiation";
        if (isActive && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            return (false, "Expected Close Date cannot be in the past.", null);

        if (!IsValidStageTransition(existing.Stage, opportunity.Stage))
            return (false, $"Invalid transition from {existing.Stage} to {opportunity.Stage}.", null);

        var oldState = new
        {
            existing.OpportunityName,
            existing.Amount,
            existing.Stage,
            existing.Probability,
            existing.ExpectedCloseDate,
            existing.Status,
            existing.AssignedTo,
            existing.Notes
        };

        existing.OpportunityName = opportunity.OpportunityName.Trim();
        existing.Amount = opportunity.Amount;
        existing.Stage = opportunity.Stage;
        existing.Probability = opportunity.Probability;
        existing.ExpectedCloseDate = opportunity.ExpectedCloseDate;
        existing.Source = opportunity.Source?.Trim();
        existing.Notes = opportunity.Notes?.Trim();

        existing.Status = opportunity.Stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };

        if (user.IsInRole("Admin") || user.IsInRole("Manager"))
        {
            if (!string.IsNullOrWhiteSpace(opportunity.AssignedTo))
            {
                existing.AssignedTo = opportunity.AssignedTo;
            }
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Update",
            module: "Opportunity",
            entityName: "Opportunity",
            recordId: existing.OpportunityId.ToString(),
            oldValue: oldState,
            newValue: new
            {
                existing.OpportunityName,
                existing.Amount,
                existing.Stage,
                existing.Probability,
                existing.ExpectedCloseDate,
                existing.Status,
                existing.AssignedTo,
                existing.Notes
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, existing);
    }

    public async Task<(bool Success, string? Error)> ChangeStageAsync(ClaimsPrincipal user, int id, string newStage)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessOpportunityAsync(user, opp))
            return (false, "You are not authorized to perform this action.");

        if (!IsValidStageTransition(opp.Stage, newStage))
            return (false, $"Invalid transition from {opp.Stage} to {newStage}.");

        var oldStage = opp.Stage;
        opp.Stage = newStage;
        opp.Status = newStage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };

        if (newStage == "Won")
        {
            opp.Probability = 100;
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "StageChange",
            module: "Opportunity",
            entityName: "Opportunity",
            recordId: opp.OpportunityId.ToString(),
            oldValue: new { Stage = oldStage },
            newValue: new { Stage = newStage, opp.Status, opp.Probability },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteOpportunityAsync(ClaimsPrincipal user, int id)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessOpportunityAsync(user, opp))
            return (false, "You are not authorized to perform this action.");

        var oldStatus = opp.Status;
        opp.Status = "Lost";
        opp.Stage = "Lost";
        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Deactivate",
            module: "Opportunity",
            entityName: "Opportunity",
            recordId: opp.OpportunityId.ToString(),
            oldValue: new { Status = oldStatus },
            newValue: new { Status = "Lost", Stage = "Lost" },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }
}
