using System.Security.Claims;
using System.Text.RegularExpressions;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;
    private readonly IAuditService _auditService;

    public CustomerService(
        ApplicationDbContext context,
        IResourceAuthorizationService authService,
        IAuditService auditService)
    {
        _context = context;
        _authService = authService;
        _auditService = auditService;
    }

    public async Task<PagedList<Customer>> GetCustomersAsync(
        ClaimsPrincipal user,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 10)
    {
        var query = _context.Customers
            .Include(c => c.Owner)
            .AsNoTracking();

        query = await _authService.FilterCustomersAsync(user, query);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.CustomerName.Contains(term) ||
                c.Email.Contains(term) ||
                c.Phone.Contains(term) ||
                (c.CompanyName != null && c.CompanyName.Contains(term)) ||
                c.CustomerCode.Contains(term));
        }

        bool isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLower()) switch
        {
            "name" => isDescending ? query.OrderByDescending(c => c.CustomerName) : query.OrderBy(c => c.CustomerName),
            "email" => isDescending ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
            "phone" => isDescending ? query.OrderByDescending(c => c.Phone) : query.OrderBy(c => c.Phone),
            "company" => isDescending ? query.OrderByDescending(c => c.CompanyName) : query.OrderBy(c => c.CompanyName),
            "status" => isDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "owner" => isDescending ? query.OrderByDescending(c => c.Owner != null ? c.Owner.FullName : "") : query.OrderBy(c => c.Owner != null ? c.Owner.FullName : ""),
            _ => isDescending ? query.OrderBy(c => c.CreatedDate) : query.OrderByDescending(c => c.CreatedDate)
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

        return new PagedList<Customer>
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
                Controller = "Customers",
                Action = "Index"
            }
        };
    }

    public async Task<Customer?> GetCustomerByIdAsync(ClaimsPrincipal user, int id)
    {
        var customer = await _context.Customers
            .Include(c => c.Owner)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps)
            .Include(c => c.Activities)
            .FirstOrDefaultAsync(c => c.CustomerId == id);

        if (customer == null) return null;

        if (!await _authService.CanAccessCustomerAsync(user, customer))
        {
            return null; // Deny access
        }

        return customer;
    }

    public async Task<bool> IsEmailDuplicateAsync(string email, int? excludeCustomerId = null)
    {
        var trimmed = email.Trim().ToLower();
        var query = _context.Customers.Where(c => c.Email.ToLower() == trimmed);
        if (excludeCustomerId.HasValue)
        {
            query = query.Where(c => c.CustomerId != excludeCustomerId.Value);
        }
        return await query.AnyAsync();
    }

    public async Task<bool> IsPhoneDuplicateAsync(string phone, int? excludeCustomerId = null)
    {
        var trimmed = phone.Trim();
        var query = _context.Customers.Where(c => c.Phone == trimmed);
        if (excludeCustomerId.HasValue)
        {
            query = query.Where(c => c.CustomerId != excludeCustomerId.Value);
        }
        return await query.AnyAsync();
    }

    public async Task<(bool Success, string? Error, Customer? Customer)> CreateCustomerAsync(ClaimsPrincipal user, Customer customer)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(customer.CustomerName))
            return (false, "Customer name is required.", null);

        customer.CustomerName = customer.CustomerName.Trim();
        customer.Email = customer.Email.Trim().ToLower();
        customer.Phone = customer.Phone.Trim();

        if (string.IsNullOrWhiteSpace(customer.Email) || !customer.Email.Contains('@'))
            return (false, "Enter a valid email address.", null);

        if (!Regex.IsMatch(customer.Phone, @"^[6-9]\d{9}$"))
            return (false, "Enter a valid phone number.", null);

        if (await IsEmailDuplicateAsync(customer.Email))
            return (false, "A customer with this email address already exists.", null);

        if (await IsPhoneDuplicateAsync(customer.Phone))
            return (false, "A customer with this phone number already exists.", null);

        // 2. Ownership
        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!user.IsInRole("Admin") && !user.IsInRole("Manager"))
        {
            customer.OwnerId = currentUserId;
        }
        else if (string.IsNullOrWhiteSpace(customer.OwnerId))
        {
            customer.OwnerId = currentUserId;
        }

        // 3. Code Generation
        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
        {
            var count = await _context.Customers.CountAsync() + 1;
            customer.CustomerCode = $"CUST-{count:D4}";
        }

        customer.CreatedDate = DateTime.UtcNow;
        customer.CreatedBy = user.Identity?.Name ?? currentUserId;
        customer.Status = "Active";

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // 4. Audit
        await _auditService.LogAsync(
            action: "Create",
            module: "Customer",
            entityName: "Customer",
            recordId: customer.CustomerId.ToString(),
            oldValue: null,
            newValue: new
            {
                customer.CustomerId,
                customer.CustomerCode,
                customer.CustomerName,
                customer.Email,
                customer.Phone,
                customer.CompanyName,
                customer.OwnerId
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, customer);
    }

    public async Task<(bool Success, string? Error, Customer? Customer)> UpdateCustomerAsync(ClaimsPrincipal user, Customer customer)
    {
        var existing = await _context.Customers.FindAsync(customer.CustomerId);
        if (existing == null)
            return (false, "Requested record was not found.", null);

        if (!await _authService.CanAccessCustomerAsync(user, existing))
            return (false, "You are not authorized to perform this action.", null);

        if (string.IsNullOrWhiteSpace(customer.CustomerName))
            return (false, "Customer name is required.", null);

        var trimmedEmail = customer.Email.Trim().ToLower();
        var trimmedPhone = customer.Phone.Trim();

        if (string.IsNullOrWhiteSpace(trimmedEmail) || !trimmedEmail.Contains('@'))
            return (false, "Enter a valid email address.", null);

        if (!Regex.IsMatch(trimmedPhone, @"^[6-9]\d{9}$"))
            return (false, "Enter a valid phone number.", null);

        if (await IsEmailDuplicateAsync(trimmedEmail, existing.CustomerId))
            return (false, "A customer with this email address already exists.", null);

        if (await IsPhoneDuplicateAsync(trimmedPhone, existing.CustomerId))
            return (false, "A customer with this phone number already exists.", null);

        var oldState = new
        {
            existing.CustomerName,
            existing.Email,
            existing.Phone,
            existing.CompanyName,
            existing.Address,
            existing.City,
            existing.State,
            existing.Notes,
            existing.OwnerId,
            existing.Status
        };

        existing.CustomerName = customer.CustomerName.Trim();
        existing.Email = trimmedEmail;
        existing.Phone = trimmedPhone;
        existing.CompanyName = customer.CompanyName?.Trim();
        existing.Address = customer.Address?.Trim();
        existing.City = customer.City?.Trim();
        existing.State = customer.State?.Trim();
        existing.Notes = customer.Notes?.Trim();
        existing.ModifiedDate = DateTime.UtcNow;

        if (user.IsInRole("Admin") || user.IsInRole("Manager"))
        {
            if (!string.IsNullOrWhiteSpace(customer.OwnerId))
            {
                existing.OwnerId = customer.OwnerId;
            }
        }

        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Update",
            module: "Customer",
            entityName: "Customer",
            recordId: existing.CustomerId.ToString(),
            oldValue: oldState,
            newValue: new
            {
                existing.CustomerName,
                existing.Email,
                existing.Phone,
                existing.CompanyName,
                existing.Address,
                existing.City,
                existing.State,
                existing.Notes,
                existing.OwnerId,
                existing.Status
            },
            result: "Success",
            userId: currentUserId);

        return (true, null, existing);
    }

    public async Task<(bool Success, string? Error)> DeactivateCustomerAsync(ClaimsPrincipal user, int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return (false, "Requested record was not found.");

        if (!await _authService.CanAccessCustomerAsync(user, customer))
            return (false, "You are not authorized to perform this action.");

        var oldStatus = customer.Status;
        customer.Status = "Inactive";
        customer.ModifiedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _auditService.LogAsync(
            action: "Deactivate",
            module: "Customer",
            entityName: "Customer",
            recordId: customer.CustomerId.ToString(),
            oldValue: new { Status = oldStatus },
            newValue: new { Status = "Inactive" },
            result: "Success",
            userId: currentUserId);

        return (true, null);
    }
}
