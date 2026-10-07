using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class ActivityService : IActivityService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;

    public ActivityService(
        ApplicationDbContext context,
        IResourceAuthorizationService authService,
        IAuditService auditService)
    {
        _context = context;
        _authService = authService;
        _auditService = auditService;
    }

    public async Task<PagedList<Activity>> GetActivitiesAsync(
        ClaimsPrincipal user,
        string? type = null,
        string? status = null,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10)
    {
        var query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsNoTracking();

        query = await _authService.FilterActivitiesAsync(user, query);

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                a.Subject.Contains(term) ||
                (a.Description != null && a.Description.Contains(term)) ||
                (a.Customer != null && a.Customer.CustomerName.Contains(term)) ||
                (a.Lead != null && a.Lead.LeadName.Contains(term)));
        }

        bool isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLower()) switch
        {
            "subject" => isDescending ? query.OrderByDescending(a => a.Subject) : query.OrderBy(a => a.Subject),
            "type" => isDescending ? query.OrderByDescending(a => a.ActivityType) : query.OrderBy(a => a.ActivityType),
            "status" => isDescending ? query.OrderByDescending(a => a.Status) : query.OrderBy(a => a.Status),
            "assigned" => isDescending ? query.OrderByDescending(a => a.AssignedUser != null ? a.AssignedUser.FullName : "") : query.OrderBy(a => a.AssignedUser != null ? a.AssignedUser.FullName : ""),
            _ => isDescending ? query.OrderByDescending(a => a.ActivityDate) : query.OrderBy(a => a.ActivityDate)
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

        return new PagedList<Activity>
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
                Controller = "Activities",
                Action = "Index"
            }
        };
    }

    public async Task<Activity?> GetActivityByIdAsync(ClaimsPrincipal user, int id)
    {
        var activity = await _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null) return null;

        if (!await _authService.CanAccessActivityAsync(user, activity))
        {
            return null; // Deny
        }

        return activity;
    }

    public async Task<(bool Success, string? Error, Activity? Activity)> CreateActivityAsync(ClaimsPrincipal user, Activity activity)
    {
        if (string.IsNullOrWhiteSpace(activity.Subject))
            return (false, "Subject is required.", null);

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!user.IsInRole("Admin") && !user.IsInRole("Manager"))
        {
            activity.AssignedTo = currentUserId;
        }
        else if (string.IsNullOrWhiteSpace(activity.AssignedTo))
        {
            activity.AssignedTo = currentUserId;
        }

        activity.CreatedDate = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(activity.Status))
            activity.Status = "Completed";

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Create",
            module: "Activity",
            entityName: "Activity",
            recordId: activity.ActivityId.ToString(),
            oldValue: null,
            newValue: new
            {
                activity.ActivityId,
                activity.Subject,
                activity.ActivityType,
                activity.ActivityDate,
                activity.Status,
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, activity);
    }

    public async Task<(bool Success, string? Error, Activity? Activity)> UpdateActivityAsync(ClaimsPrincipal user, Activity activity)
    {
        var existing = await _context.Activities.FindAsync(activity.ActivityId);
        if (existing == null)
            return (false, "Requested record was not found.", null);

        if (!await _authService.CanAccessActivityAsync(user, existing))
            return (false, "You are not authorized to perform this action.", null);

        if (string.IsNullOrWhiteSpace(activity.Subject))
            return (false, "Subject is required.", null);

        var oldState = new
        {
            existing.Subject,
            existing.ActivityType,
            existing.ActivityDate,
            existing.Status,
            existing.Description,
            existing.CustomerId,
            existing.LeadId
        };

        existing.Subject = activity.Subject.Trim();
        existing.ActivityType = activity.ActivityType;
        existing.ActivityDate = activity.ActivityDate;
        existing.Status = activity.Status;
        existing.Description = activity.Description?.Trim();
        existing.CustomerId = activity.CustomerId;
        existing.LeadId = activity.LeadId;

        if (user.IsInRole("Admin") || user.IsInRole("Manager"))
        {
            if (!string.IsNullOrWhiteSpace(activity.AssignedTo))
            {
                existing.AssignedTo = activity.AssignedTo;
            }
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Update",
            module: "Activity",
            entityName: "Activity",
            recordId: existing.ActivityId.ToString(),
            oldValue: oldState,
            newValue: new
            {
                existing.Subject,
                existing.ActivityType,
                existing.ActivityDate,
                existing.Status,
                existing.Description,
                existing.CustomerId,
                existing.LeadId
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, existing);
    }

    public async Task<(bool Success, string? Error)> DeleteActivityAsync(ClaimsPrincipal user, int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessActivityAsync(user, activity))
            return (false, "You are not authorized to perform this action.");

        var oldSubject = activity.Subject;
        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Delete",
            module: "Activity",
            entityName: "Activity",
            recordId: id.ToString(),
            oldValue: new { Subject = oldSubject },
            newValue: null,
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }
}
