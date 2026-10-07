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
public class LeadsController : Controller
{
    private readonly ILeadService _leadService;
    private readonly ApplicationDbContext _context;

    public LeadsController(ILeadService leadService, ApplicationDbContext context)
    {
        _leadService = leadService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? priority,
        string? sortBy,
        string? sortOrder,
        int page = 1)
    {
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentPriority = priority;
        var model = await _leadService.GetLeadsAsync(User, search, status, priority, sortBy, sortOrder, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(User, id);
        if (lead == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }
        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        return View(new Lead { Status = "New", Priority = "Medium" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(lead.AssignedTo);
            return View(lead);
        }

        var (success, error, created) = await _leadService.CreateLeadAsync(User, lead);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateUsersDropdownAsync(lead.AssignedTo);
            return View(lead);
        }

        TempData["SuccessMessage"] = $"Lead '{created!.LeadName}' was created successfully.";
        return RedirectToAction(nameof(Details), new { id = created.LeadId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(User, id);
        if (lead == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdownAsync(lead.AssignedTo);
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId)
        {
            TempData["ErrorMessage"] = "Requested record was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(lead.AssignedTo);
            return View(lead);
        }

        var (success, error, updated) = await _leadService.UpdateLeadAsync(User, lead);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateUsersDropdownAsync(lead.AssignedTo);
            return View(lead);
        }

        TempData["SuccessMessage"] = $"Lead '{updated!.LeadName}' was updated successfully.";
        return RedirectToAction(nameof(Details), new { id = updated.LeadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string newStatus)
    {
        var (success, error) = await _leadService.ChangeStatusAsync(User, id, newStatus);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to change lead status.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Lead status updated to '{newStatus}'.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _leadService.GetLeadByIdAsync(User, id);
        if (lead == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        if (lead.Status != "Qualified")
        {
            TempData["WarningMessage"] = "Only qualified leads can be converted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new ConvertLeadViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            CustomerName = lead.LeadName,
            OpportunityName = $"{lead.LeadName} - Deal",
            Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            CreateOpportunity = true
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(ConvertLeadViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (success, error, customer, opp) = await _leadService.ConvertLeadAsync(User, model);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Conversion failed.");
            return View(model);
        }

        TempData["SuccessMessage"] = $"Lead converted successfully into Customer '{customer!.CustomerName}'!";
        return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var (success, error) = await _leadService.DeleteLeadAsync(User, id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to mark lead as lost.";
        }
        else
        {
            TempData["SuccessMessage"] = "Lead marked as Lost.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdownAsync(string? selectedUserId = null)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToListAsync();

            ViewBag.Users = new SelectList(users, "Id", "FullName", selectedUserId);
        }
    }
}
