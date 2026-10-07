using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class FollowUpsController : Controller
{
    private readonly IFollowUpService _followUpService;
    private readonly ApplicationDbContext _context;

    public FollowUpsController(IFollowUpService followUpService, ApplicationDbContext context)
    {
        _followUpService = followUpService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? filter,
        string? search,
        string? sortBy,
        string? sortOrder,
        int page = 1)
    {
        ViewBag.CurrentFilter = filter ?? "all";
        var model = await _followUpService.GetFollowUpsAsync(User, filter, search, sortBy, sortOrder, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var f = await _followUpService.GetFollowUpByIdAsync(User, id);
        if (f == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }
        return View(f);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        await PopulateDropdownsAsync(customerId, leadId, opportunityId);
        return View(new FollowUp
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            FollowUpDate = DateTime.UtcNow.AddDays(1),
            FollowUpType = "Call",
            Status = "Planned"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
            return View(followUp);
        }

        var (success, error, created) = await _followUpService.CreateFollowUpAsync(User, followUp);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
            return View(followUp);
        }

        TempData["SuccessMessage"] = $"Follow-up '{created!.Subject}' scheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? completionRemarks)
    {
        var (success, error) = await _followUpService.CompleteFollowUpAsync(User, id, completionRemarks);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to complete follow-up.";
        }
        else
        {
            TempData["SuccessMessage"] = "Follow-up marked as Completed.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Reschedule(int id)
    {
        var f = await _followUpService.GetFollowUpByIdAsync(User, id);
        if (f == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        var model = new RescheduleFollowUpViewModel
        {
            FollowUpId = f.FollowUpId,
            Subject = f.Subject,
            OldFollowUpDate = f.FollowUpDate,
            NewFollowUpDate = DateTime.UtcNow.AddDays(2)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(RescheduleFollowUpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error) = await _followUpService.RescheduleFollowUpAsync(User, model.FollowUpId, model.NewFollowUpDate, model.Reason);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to reschedule follow-up.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Follow-up rescheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (success, error) = await _followUpService.DeleteFollowUpAsync(User, id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to cancel follow-up.";
        }
        else
        {
            TempData["SuccessMessage"] = "Follow-up cancelled.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(
        int? customerId = null,
        int? leadId = null,
        int? opportunityId = null,
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

        var opps = await _context.Opportunities
            .Where(o => o.Status == "Open")
            .Select(o => new { o.OpportunityId, o.OpportunityName })
            .ToListAsync();
        ViewBag.Opportunities = new SelectList(opps, "OpportunityId", "OpportunityName", opportunityId);

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
