using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Authorization;

public class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ResourceAuthorizationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<string>> GetAccessibleUserIdsAsync(ClaimsPrincipal user)
    {
        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return new List<string>();
        }

        if (user.IsInRole("Admin"))
        {
            // Admin has access to all users
            return await _context.Users.Select(u => u.Id).ToListAsync();
        }

        if (user.IsInRole("Manager"))
        {
            // Manager + Direct Reports
            var reportIds = await _context.Users
                .Where(u => u.ManagerId == currentUserId)
                .Select(u => u.Id)
                .ToListAsync();

            reportIds.Add(currentUserId);
            return reportIds;
        }

        // SalesExecutive: Own ID only
        return new List<string> { currentUserId };
    }

    public async Task<bool> CanAccessUserAsync(ClaimsPrincipal user, string targetUserId)
    {
        if (user.IsInRole("Admin")) return true;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return accessibleIds.Contains(targetUserId);
    }

    public async Task<bool> CanAccessCustomerAsync(ClaimsPrincipal user, Customer customer)
    {
        if (user.IsInRole("Admin")) return true;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return accessibleIds.Contains(customer.OwnerId);
    }

    public async Task<bool> CanAccessLeadAsync(ClaimsPrincipal user, Lead lead)
    {
        if (user.IsInRole("Admin")) return true;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return accessibleIds.Contains(lead.AssignedTo);
    }

    public async Task<bool> CanAccessOpportunityAsync(ClaimsPrincipal user, Opportunity opportunity)
    {
        if (user.IsInRole("Admin")) return true;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return accessibleIds.Contains(opportunity.AssignedTo);
    }

    public async Task<bool> CanAccessFollowUpAsync(ClaimsPrincipal user, FollowUp followUp)
    {
        if (user.IsInRole("Admin")) return true;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return accessibleIds.Contains(followUp.AssignedTo);
    }

    public async Task<bool> CanAccessActivityAsync(ClaimsPrincipal user, Activity activity)
    {
        if (user.IsInRole("Admin")) return true;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return accessibleIds.Contains(activity.AssignedTo);
    }

    public async Task<IQueryable<Customer>> FilterCustomersAsync(ClaimsPrincipal user, IQueryable<Customer> query)
    {
        if (user.IsInRole("Admin")) return query;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return query.Where(c => accessibleIds.Contains(c.OwnerId));
    }

    public async Task<IQueryable<Lead>> FilterLeadsAsync(ClaimsPrincipal user, IQueryable<Lead> query)
    {
        if (user.IsInRole("Admin")) return query;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return query.Where(l => accessibleIds.Contains(l.AssignedTo));
    }

    public async Task<IQueryable<Opportunity>> FilterOpportunitiesAsync(ClaimsPrincipal user, IQueryable<Opportunity> query)
    {
        if (user.IsInRole("Admin")) return query;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return query.Where(o => accessibleIds.Contains(o.AssignedTo));
    }

    public async Task<IQueryable<FollowUp>> FilterFollowUpsAsync(ClaimsPrincipal user, IQueryable<FollowUp> query)
    {
        if (user.IsInRole("Admin")) return query;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return query.Where(f => accessibleIds.Contains(f.AssignedTo));
    }

    public async Task<IQueryable<Activity>> FilterActivitiesAsync(ClaimsPrincipal user, IQueryable<Activity> query)
    {
        if (user.IsInRole("Admin")) return query;
        var accessibleIds = await GetAccessibleUserIdsAsync(user);
        return query.Where(a => accessibleIds.Contains(a.AssignedTo));
    }

    public async Task<IQueryable<AuditLog>> FilterAuditLogsAsync(ClaimsPrincipal user, IQueryable<AuditLog> query)
    {
        if (user.IsInRole("Admin")) return query;
        if (user.IsInRole("Manager"))
        {
            var accessibleIds = await GetAccessibleUserIdsAsync(user);
            return query.Where(al => al.UserId != null && accessibleIds.Contains(al.UserId));
        }
        // SalesExecutive: no audit log access
        return query.Where(al => false);
    }
}
