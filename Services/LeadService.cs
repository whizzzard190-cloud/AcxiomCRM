using System.Security.Claims;
using System.Text.RegularExpressions;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace AcxiomCRM.Services;

public class LeadService : ILeadService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LeadService>? _logger;

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "New", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Contacted", "Lost" } },
        { "Contacted", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Qualified", "Unqualified", "Lost" } },
        { "Qualified", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Converted", "Lost" } },
        { "Unqualified", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Contacted", "Lost" } },
        { "Converted", new HashSet<string>(StringComparer.OrdinalIgnoreCase) }, // Terminal
        { "Lost", new HashSet<string>(StringComparer.OrdinalIgnoreCase) }       // Terminal
    };

    public LeadService(
        ApplicationDbContext context,
        IResourceAuthorizationService authService,
        IAuditService auditService,
        IConfiguration configuration,
        ILogger<LeadService>? logger = null)
    {
        _context = context;
        _authService = authService;
        _auditService = auditService;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsValidStatus(string status)
    {
        return !string.IsNullOrWhiteSpace(status) && ValidStatuses.Contains(status);
    }

    public bool IsValidTransition(string currentStatus, string nextStatus)
    {
        if (!IsValidStatus(currentStatus) || !IsValidStatus(nextStatus))
            return false;

        if (string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
            return true;

        if (AllowedTransitions.TryGetValue(currentStatus, out var allowedNext))
        {
            return allowedNext.Contains(nextStatus);
        }

        return false;
    }

    public async Task<PagedList<Lead>> GetLeadsAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? status = null,
        string? priority = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10)
    {
        var query = _context.Leads
            .Include(l => l.AssignedUser)
            .AsNoTracking();

        query = await _authService.FilterLeadsAsync(user, query);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                l.LeadName.Contains(term) ||
                l.Email.Contains(term) ||
                l.Phone.Contains(term) ||
                (l.CompanyName != null && l.CompanyName.Contains(term)) ||
                l.LeadCode.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        bool isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLower()) switch
        {
            "name" => isDescending ? query.OrderByDescending(l => l.LeadName) : query.OrderBy(l => l.LeadName),
            "company" => isDescending ? query.OrderByDescending(l => l.CompanyName) : query.OrderBy(l => l.CompanyName),
            "status" => isDescending ? query.OrderByDescending(l => l.Status) : query.OrderBy(l => l.Status),
            "priority" => isDescending ? query.OrderByDescending(l => l.Priority) : query.OrderBy(l => l.Priority),
            "value" => isDescending ? query.OrderByDescending(l => l.ExpectedValue) : query.OrderBy(l => l.ExpectedValue),
            "assigned" => isDescending ? query.OrderByDescending(l => l.AssignedUser != null ? l.AssignedUser.FullName : "") : query.OrderBy(l => l.AssignedUser != null ? l.AssignedUser.FullName : ""),
            _ => isDescending ? query.OrderBy(l => l.CreatedDate) : query.OrderByDescending(l => l.CreatedDate)
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

        return new PagedList<Lead>
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
                Controller = "Leads",
                Action = "Index"
            }
        };
    }

    public async Task<Lead?> GetLeadByIdAsync(ClaimsPrincipal user, int id)
    {
        var lead = await _context.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.ConvertedOpportunity)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null) return null;

        if (!await _authService.CanAccessLeadAsync(user, lead))
        {
            return null; // Scope denied
        }

        return lead;
    }

    public async Task<(bool Success, string? Error, Lead? Lead)> CreateLeadAsync(ClaimsPrincipal user, Lead lead)
    {
        if (string.IsNullOrWhiteSpace(lead.LeadName))
            return (false, "Lead name is required.", null);

        lead.LeadName = lead.LeadName.Trim();
        lead.Email = lead.Email.Trim().ToLower();
        lead.Phone = lead.Phone.Trim();

        if (string.IsNullOrWhiteSpace(lead.Email) || !lead.Email.Contains('@'))
            return (false, "Enter a valid email address.", null);

        if (!Regex.IsMatch(lead.Phone, @"^[6-9]\d{9}$"))
            return (false, "Enter a valid phone number.", null);

        decimal maxVal = _configuration.GetValue<decimal>("CrmSettings:MaxExpectedValue", 100000000m);
        if (lead.ExpectedValue < 0 || lead.ExpectedValue > maxVal)
            return (false, $"Expected Value must be between 0 and {maxVal:N0}.", null);

        if (string.IsNullOrWhiteSpace(lead.Status))
            lead.Status = "New";

        if (!IsValidStatus(lead.Status))
            return (false, "Invalid lead status.", null);

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!user.IsInRole("Admin") && !user.IsInRole("Manager"))
        {
            lead.AssignedTo = currentUserId;
        }
        else if (string.IsNullOrWhiteSpace(lead.AssignedTo))
        {
            lead.AssignedTo = currentUserId;
        }

        if (string.IsNullOrWhiteSpace(lead.LeadCode))
        {
            var count = await _context.Leads.CountAsync() + 1;
            lead.LeadCode = $"LEAD-{count:D4}";
        }

        lead.CreatedDate = DateTime.UtcNow;

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Create",
            module: "Lead",
            entityName: "Lead",
            recordId: lead.LeadId.ToString(),
            oldValue: null,
            newValue: new
            {
                lead.LeadId,
                lead.LeadCode,
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.Status,
                lead.Priority,
                lead.ExpectedValue,
                lead.AssignedTo
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, lead);
    }

    public async Task<(bool Success, string? Error, Lead? Lead)> UpdateLeadAsync(ClaimsPrincipal user, Lead lead)
    {
        var existing = await _context.Leads.FindAsync(lead.LeadId);
        if (existing == null)
            return (false, "Requested record was not found.", null);

        if (!await _authService.CanAccessLeadAsync(user, existing))
            return (false, "You are not authorized to perform this action.", null);

        if (string.IsNullOrWhiteSpace(lead.LeadName))
            return (false, "Lead name is required.", null);

        var trimmedEmail = lead.Email.Trim().ToLower();
        var trimmedPhone = lead.Phone.Trim();

        if (string.IsNullOrWhiteSpace(trimmedEmail) || !trimmedEmail.Contains('@'))
            return (false, "Enter a valid email address.", null);

        if (!Regex.IsMatch(trimmedPhone, @"^[6-9]\d{9}$"))
            return (false, "Enter a valid phone number.", null);

        decimal maxVal = _configuration.GetValue<decimal>("CrmSettings:MaxExpectedValue", 100000000m);
        if (lead.ExpectedValue < 0 || lead.ExpectedValue > maxVal)
            return (false, $"Expected Value must be between 0 and {maxVal:N0}.", null);

        if (!IsValidStatus(lead.Status))
            return (false, "Invalid lead status.", null);

        if (!IsValidTransition(existing.Status, lead.Status))
            return (false, $"Invalid lead status transition from {existing.Status} to {lead.Status}.", null);

        var oldState = new
        {
            existing.LeadName,
            existing.Email,
            existing.Phone,
            existing.CompanyName,
            existing.Source,
            existing.Status,
            existing.Priority,
            existing.ExpectedValue,
            existing.Notes,
            existing.AssignedTo
        };

        existing.LeadName = lead.LeadName.Trim();
        existing.Email = trimmedEmail;
        existing.Phone = trimmedPhone;
        existing.CompanyName = lead.CompanyName?.Trim();
        existing.Source = lead.Source?.Trim();
        existing.Status = lead.Status;
        existing.Priority = lead.Priority;
        existing.ExpectedValue = lead.ExpectedValue;
        existing.Notes = lead.Notes?.Trim();

        if (user.IsInRole("Admin") || user.IsInRole("Manager"))
        {
            if (!string.IsNullOrWhiteSpace(lead.AssignedTo))
            {
                existing.AssignedTo = lead.AssignedTo;
            }
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Update",
            module: "Lead",
            entityName: "Lead",
            recordId: existing.LeadId.ToString(),
            oldValue: oldState,
            newValue: new
            {
                existing.LeadName,
                existing.Email,
                existing.Phone,
                existing.CompanyName,
                existing.Source,
                existing.Status,
                existing.Priority,
                existing.ExpectedValue,
                existing.Notes,
                existing.AssignedTo
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, existing);
    }

    public async Task<(bool Success, string? Error)> ChangeStatusAsync(ClaimsPrincipal user, int leadId, string newStatus)
    {
        var lead = await _context.Leads.FindAsync(leadId);
        if (lead == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessLeadAsync(user, lead))
            return (false, "You are not authorized to perform this action.");

        if (!IsValidStatus(newStatus))
            return (false, "Invalid lead status.");

        if (!IsValidTransition(lead.Status, newStatus))
            return (false, $"Invalid lead status transition from {lead.Status} to {newStatus}.");

        var oldStatus = lead.Status;
        lead.Status = newStatus;
        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "StatusChange",
            module: "Lead",
            entityName: "Lead",
            recordId: lead.LeadId.ToString(),
            oldValue: new { Status = oldStatus },
            newValue: new { Status = newStatus },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error, Customer? Customer, Opportunity? Opportunity)> ConvertLeadAsync(ClaimsPrincipal user, ConvertLeadViewModel model)
    {
        var lead = await _context.Leads.FindAsync(model.LeadId);
        if (lead == null)
            return (false, "Requested record was not found.", null, null);

        if (!await _authService.CanAccessLeadAsync(user, lead))
            return (false, "You are not authorized to perform this action.", null, null);

        if (lead.Status != "Qualified")
            return (false, "Only qualified leads can be converted.", null, null);

        if (string.IsNullOrWhiteSpace(model.CustomerName))
            return (false, "Customer name is required.", null, null);

        if (model.CreateOpportunity)
        {
            if (string.IsNullOrWhiteSpace(model.OpportunityName))
                return (false, "Opportunity name is required when creating an opportunity.", null, null);

            if (model.Amount <= 0)
                return (false, "Opportunity Amount must be greater than 0.", null, null);

            if (model.Probability < 0 || model.Probability > 100)
                return (false, "Probability must be between 0 and 100.", null, null);

            if (model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
                return (false, "Expected Close Date cannot be in the past.", null, null);
        }

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Create or Find duplicate Customer by Email or Phone
            var leadEmail = lead.Email.Trim().ToLower();
            var leadPhone = lead.Phone.Trim();

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email.ToLower() == leadEmail || c.Phone == leadPhone);

            if (customer == null)
            {
                var custCount = await _context.Customers.CountAsync() + 1;
                customer = new Customer
                {
                    CustomerCode = $"CUST-{custCount:D4}",
                    CustomerName = model.CustomerName.Trim(),
                    Email = leadEmail,
                    Phone = leadPhone,
                    CompanyName = model.CompanyName ?? lead.CompanyName,
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Status = "Active",
                    Notes = $"Converted from Lead {lead.LeadCode}. {lead.Notes}",
                    OwnerId = lead.AssignedTo,
                    CreatedBy = user.Identity?.Name ?? currentUserId,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // 2. Optionally create Opportunity
            Opportunity? opportunity = null;
            if (model.CreateOpportunity)
            {
                opportunity = new Opportunity
                {
                    OpportunityName = model.OpportunityName!.Trim(),
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.Amount,
                    Stage = "Qualification",
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate ?? DateTime.UtcNow.AddDays(30),
                    Status = "Open",
                    Source = lead.Source ?? "Lead Conversion",
                    Notes = $"Created upon conversion of Lead {lead.LeadCode}",
                    AssignedTo = lead.AssignedTo,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Opportunities.Add(opportunity);
                await _context.SaveChangesAsync();
            }

            // 3. Mark Lead as Converted
            lead.Status = "Converted";
            lead.ConvertedCustomerId = customer.CustomerId;
            if (opportunity != null)
            {
                lead.ConvertedOpportunityId = opportunity.OpportunityId;
            }
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // 4. Audit Lead Conversion
            await _auditService.LogAsync(
                action: "Convert",
                module: "Lead",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: new { Status = "Qualified" },
                newValue: new
                {
                    Status = "Converted",
                    CustomerId = customer.CustomerId,
                    CustomerName = customer.CustomerName,
                    OpportunityId = opportunity?.OpportunityId,
                    OpportunityName = opportunity?.OpportunityName
                },
                result: "Success",
                userId: currentUserId);

            return (true, null, customer, opportunity);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "Lead conversion transaction failed for Lead ID {LeadId}", model.LeadId);
            return (false, "An error occurred while converting the lead. Please try again.", null, null);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteLeadAsync(ClaimsPrincipal user, int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessLeadAsync(user, lead))
            return (false, "You are not authorized to perform this action.");

        var oldStatus = lead.Status;
        lead.Status = "Lost";
        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Deactivate",
            module: "Lead",
            entityName: "Lead",
            recordId: lead.LeadId.ToString(),
            oldValue: new { Status = oldStatus },
            newValue: new { Status = "Lost" },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }
}
