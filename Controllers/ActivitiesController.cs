using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly IActivityService _activityService;
    private readonly ApplicationDbContext _context;

    public ActivitiesController(IActivityService activityService, ApplicationDbContext context)
    {
        _activityService = activityService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? type,
        string? status,
        string? search,
        string? sortBy,
        string? sortOrder,
        int page = 1)
    {
        ViewBag.CurrentType = type;
        ViewBag.CurrentStatus = status;
        var model = await _activityService.GetActivitiesAsync(User, type, status, search, sortBy, sortOrder, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var act = await _activityService.GetActivityByIdAsync(User, id);
        if (act == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }
        return View(act);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId)
    {
        await PopulateDropdownsAsync(customerId, leadId);
        return View(new Activity
        {
            CustomerId = customerId,
            LeadId = leadId,
            ActivityDate = DateTime.UtcNow,
            ActivityType = "Call",
            Status = "Completed"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.AssignedTo);
            return View(activity);
        }

        var (success, error, created) = await _activityService.CreateActivityAsync(User, activity);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.AssignedTo);
            return View(activity);
        }

        TempData["SuccessMessage"] = $"Activity '{created!.Subject}' recorded successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var act = await _activityService.GetActivityByIdAsync(User, id);
        if (act == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(act.CustomerId, act.LeadId, act.AssignedTo);
        return View(act);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Activity activity)
    {
        if (id != activity.ActivityId)
        {
            TempData["ErrorMessage"] = "Requested record was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.AssignedTo);
            return View(activity);
        }

        var (success, error, updated) = await _activityService.UpdateActivityAsync(User, activity);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.AssignedTo);
            return View(activity);
        }

        TempData["SuccessMessage"] = $"Activity '{updated!.Subject}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _activityService.DeleteActivityAsync(User, id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to delete activity.";
        }
        else
        {
            TempData["SuccessMessage"] = "Activity deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(
        int? customerId = null,
        int? leadId = null,
        string? assignedTo = null)
    {
        var customers = await _context.Customers
            .Where(c => c.Status == "Active")
            .Select(c => new { c.CustomerId, c.CustomerName })
            .ToListAsync();
        ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName", customerId);

        var leads = await _context.Leads
            .Where(l => l.Status != "Converted" && l.Status != "Lost")
            .Select(l => new { l.LeadId, l.LeadName })
            .ToListAsync();
        ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName", leadId);

        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName", assignedTo);
        }
    }
}
