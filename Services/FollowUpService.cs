using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;

    public FollowUpService(
        ApplicationDbContext context,
        IResourceAuthorizationService authService,
        IAuditService auditService)
    {
        _context = context;
        _authService = authService;
        _auditService = auditService;
    }

    public async Task<PagedList<FollowUp>> GetFollowUpsAsync(
        ClaimsPrincipal user,
        string? filter = null,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10)
    {
        var query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedUser)
            .AsNoTracking();

        query = await _authService.FilterFollowUpsAsync(user, query);

        var today = DateTime.UtcNow.Date;

        query = (filter?.ToLower()) switch
        {
            "overdue" => query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date < today),
            "upcoming" => query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date >= today),
            "completed" => query.Where(f => f.Status == "Completed"),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(f =>
                f.Subject.Contains(term) ||
                (f.Customer != null && f.Customer.CustomerName.Contains(term)) ||
                (f.Lead != null && f.Lead.LeadName.Contains(term)) ||
                (f.Opportunity != null && f.Opportunity.OpportunityName.Contains(term)) ||
                (f.Remarks != null && f.Remarks.Contains(term)));
        }

        bool isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLower()) switch
        {
            "subject" => isDescending ? query.OrderByDescending(f => f.Subject) : query.OrderBy(f => f.Subject),
            "type" => isDescending ? query.OrderByDescending(f => f.FollowUpType) : query.OrderBy(f => f.FollowUpType),
            "status" => isDescending ? query.OrderByDescending(f => f.Status) : query.OrderBy(f => f.Status),
            "assigned" => isDescending ? query.OrderByDescending(f => f.AssignedUser != null ? f.AssignedUser.FullName : "") : query.OrderBy(f => f.AssignedUser != null ? f.AssignedUser.FullName : ""),
            _ => isDescending ? query.OrderByDescending(f => f.FollowUpDate) : query.OrderBy(f => f.FollowUpDate)
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

        return new PagedList<FollowUp>
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
                Controller = "FollowUps",
                Action = "Index",
                RouteValues = !string.IsNullOrEmpty(filter) ? new Dictionary<string, string> { { "filter", filter } } : new()
            }
        };
    }

    public async Task<FollowUp?> GetFollowUpByIdAsync(ClaimsPrincipal user, int id)
    {
        var f = await _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedUser)
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (f == null) return null;

        if (!await _authService.CanAccessFollowUpAsync(user, f))
        {
            return null; // Deny
        }

        return f;
    }

    public async Task<(bool Success, string? Error, FollowUp? FollowUp)> CreateFollowUpAsync(ClaimsPrincipal user, FollowUp followUp)
    {
        if (string.IsNullOrWhiteSpace(followUp.Subject))
            return (false, "Subject is required.", null);

        if (followUp.CustomerId == null && followUp.LeadId == null && followUp.OpportunityId == null)
            return (false, "At least one association (Customer, Lead, or Opportunity) is required.", null);

        // Date check: Follow-up date cannot be earlier than today
        if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            return (false, "Follow-up date cannot be earlier than today.", null);

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!user.IsInRole("Admin") && !user.IsInRole("Manager"))
        {
            followUp.AssignedTo = currentUserId;
        }
        else if (string.IsNullOrWhiteSpace(followUp.AssignedTo))
        {
            followUp.AssignedTo = currentUserId;
        }

        followUp.CreatedDate = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(followUp.Status))
            followUp.Status = "Planned";

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Create",
            module: "FollowUp",
            entityName: "FollowUp",
            recordId: followUp.FollowUpId.ToString(),
            oldValue: null,
            newValue: new
            {
                followUp.FollowUpId,
                followUp.Subject,
                followUp.FollowUpType,
                followUp.FollowUpDate,
                followUp.Status,
                followUp.CustomerId,
                followUp.LeadId,
                followUp.OpportunityId,
                followUp.AssignedTo
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, followUp);
    }

    public async Task<(bool Success, string? Error)> CompleteFollowUpAsync(ClaimsPrincipal user, int id, string? completionRemarks = null)
    {
        var f = await _context.FollowUps.FindAsync(id);
        if (f == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessFollowUpAsync(user, f))
            return (false, "You are not authorized to perform this action.");

        var oldStatus = f.Status;
        f.Status = "Completed";
        f.CompletedDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(completionRemarks))
        {
            f.Remarks = string.IsNullOrEmpty(f.Remarks) 
                ? $"[Completed]: {completionRemarks}" 
                : $"{f.Remarks}\n[Completed]: {completionRemarks}";
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Complete",
            module: "FollowUp",
            entityName: "FollowUp",
            recordId: f.FollowUpId.ToString(),
            oldValue: new { Status = oldStatus },
            newValue: new { Status = "Completed", f.CompletedDate, CompletionRemarks = completionRemarks },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RescheduleFollowUpAsync(ClaimsPrincipal user, int id, DateTime newDate, string? reason = null)
    {
        var f = await _context.FollowUps.FindAsync(id);
        if (f == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessFollowUpAsync(user, f))
            return (false, "You are not authorized to perform this action.");

        if (newDate.Date < DateTime.UtcNow.Date)
            return (false, "Follow-up date cannot be earlier than today.");

        var oldDate = f.FollowUpDate;
        f.FollowUpDate = newDate;
        f.Status = "Planned";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            f.Remarks = string.IsNullOrEmpty(f.Remarks)
                ? $"[Rescheduled from {oldDate:g}]: {reason}"
                : $"{f.Remarks}\n[Rescheduled from {oldDate:g}]: {reason}";
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Reschedule",
            module: "FollowUp",
            entityName: "FollowUp",
            recordId: f.FollowUpId.ToString(),
            oldValue: new { FollowUpDate = oldDate },
            newValue: new { FollowUpDate = newDate, Reason = reason },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteFollowUpAsync(ClaimsPrincipal user, int id)
    {
        var f = await _context.FollowUps.FindAsync(id);
        if (f == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessFollowUpAsync(user, f))
            return (false, "You are not authorized to perform this action.");

        var oldStatus = f.Status;
        f.Status = "Cancelled";
        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Deactivate",
            module: "FollowUp",
            entityName: "FollowUp",
            recordId: f.FollowUpId.ToString(),
            oldValue: new { Status = oldStatus },
            newValue: new { Status = "Cancelled" },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }
}
