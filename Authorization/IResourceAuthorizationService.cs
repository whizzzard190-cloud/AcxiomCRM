using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Authorization;

public interface IResourceAuthorizationService
{
    Task<bool> CanAccessUserAsync(ClaimsPrincipal user, string targetUserId);
    Task<bool> CanAccessCustomerAsync(ClaimsPrincipal user, Customer customer);
    Task<bool> CanAccessLeadAsync(ClaimsPrincipal user, Lead lead);
    Task<bool> CanAccessOpportunityAsync(ClaimsPrincipal user, Opportunity opportunity);
    Task<bool> CanAccessFollowUpAsync(ClaimsPrincipal user, FollowUp followUp);
    Task<bool> CanAccessActivityAsync(ClaimsPrincipal user, Activity activity);

    Task<List<string>> GetAccessibleUserIdsAsync(ClaimsPrincipal user);

    Task<IQueryable<Customer>> FilterCustomersAsync(ClaimsPrincipal user, IQueryable<Customer> query);
    Task<IQueryable<Lead>> FilterLeadsAsync(ClaimsPrincipal user, IQueryable<Lead> query);
    Task<IQueryable<Opportunity>> FilterOpportunitiesAsync(ClaimsPrincipal user, IQueryable<Opportunity> query);
    Task<IQueryable<FollowUp>> FilterFollowUpsAsync(ClaimsPrincipal user, IQueryable<FollowUp> query);
    Task<IQueryable<Activity>> FilterActivitiesAsync(ClaimsPrincipal user, IQueryable<Activity> query);
    Task<IQueryable<AuditLog>> FilterAuditLogsAsync(ClaimsPrincipal user, IQueryable<AuditLog> query);
}
