using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authService;

    public AuditLogsController(ApplicationDbContext context, IResourceAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? userId,
        string? module,
        string? actionName,
        DateTime? fromDate,
        DateTime? toDate,
        string? search,
        int page = 1)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        // Scope enforcement: Admin sees all, Manager sees team users, SE has no access
        query = await _authService.FilterAuditLogsAsync(User, query);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(a => a.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(actionName))
        {
            query = query.Where(a => a.Action == actionName);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(a => a.CreatedDate <= endOfDay);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a =>
                (a.EntityName != null && a.EntityName.Contains(term)) ||
                (a.RecordId != null && a.RecordId.Contains(term)) ||
                (a.OldValue != null && a.OldValue.Contains(term)) ||
                (a.NewValue != null && a.NewValue.Contains(term)));
        }

        int pageSize = 15;
        int totalRecords = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page < 1) page = 1;

        var items = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentUserId = userId;
        ViewBag.CurrentModule = module;
        ViewBag.CurrentAction = actionName;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        await PopulateFilterDropdownsAsync(userId);

        var model = new PagedList<AuditLog>
        {
            Items = items,
            Pager = new PagerModel
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                SearchTerm = search,
                Controller = "AuditLogs",
                Action = "Index",
                RouteValues = new Dictionary<string, string>
                {
                    { "userId", userId ?? "" },
                    { "module", module ?? "" },
                    { "actionName", actionName ?? "" },
                    { "fromDate", fromDate?.ToString("yyyy-MM-dd") ?? "" },
                    { "toDate", toDate?.ToString("yyyy-MM-dd") ?? "" }
                }
            }
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking();

        query = await _authService.FilterAuditLogsAsync(User, query);
        var log = await query.FirstOrDefaultAsync(a => a.AuditLogId == id);

        if (log == null)
        {
            TempData["ErrorMessage"] = "Audit log not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        return View(log);
    }

    private async Task PopulateFilterDropdownsAsync(string? selectedUserId)
    {
        var accessibleUserIds = await _authService.GetAccessibleUserIdsAsync(User);
        var users = await _context.Users
            .Where(u => accessibleUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "FullName", selectedUserId);

        var modules = await _context.AuditLogs
            .Select(a => a.Module)
            .Distinct()
            .ToListAsync();
        ViewBag.Modules = modules;

        var actions = await _context.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .ToListAsync();
        ViewBag.Actions = actions;
    }
}
