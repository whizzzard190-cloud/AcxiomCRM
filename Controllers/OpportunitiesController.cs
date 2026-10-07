using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private readonly IOpportunityService _opportunityService;
    private readonly ApplicationDbContext _context;

    public OpportunitiesController(IOpportunityService opportunityService, ApplicationDbContext context)
    {
        _opportunityService = opportunityService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? stage,
        string? status,
        string? sortBy,
        string? sortOrder,
        int page = 1)
    {
        ViewBag.CurrentStage = stage;
        ViewBag.CurrentStatus = status;
        var model = await _opportunityService.GetOpportunitiesAsync(User, search, stage, status, sortBy, sortOrder, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Pipeline()
    {
        var opps = await _opportunityService.GetPipelineOpportunitiesAsync(User);
        return View(opps);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var opp = await _opportunityService.GetOpportunityByIdAsync(User, id);
        if (opp == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }
        return View(opp);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId)
    {
        await PopulateDropdownsAsync(customerId);
        return View(new Opportunity
        {
            CustomerId = customerId ?? 0,
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            Amount = 10000m
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
            return View(opportunity);
        }

        var (success, error, created) = await _opportunityService.CreateOpportunityAsync(User, opportunity);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
            return View(opportunity);
        }

        TempData["SuccessMessage"] = $"Opportunity '{created!.OpportunityName}' was created successfully.";
        return RedirectToAction(nameof(Details), new { id = created.OpportunityId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var opp = await _opportunityService.GetOpportunityByIdAsync(User, id);
        if (opp == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(opp.CustomerId, opp.AssignedTo);
        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId)
        {
            TempData["ErrorMessage"] = "Requested record was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
            return View(opportunity);
        }

        var (success, error, updated) = await _opportunityService.UpdateOpportunityAsync(User, opportunity);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.AssignedTo);
            return View(opportunity);
        }

        TempData["SuccessMessage"] = $"Opportunity '{updated!.OpportunityName}' was updated successfully.";
        return RedirectToAction(nameof(Details), new { id = updated.OpportunityId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStage(int id, string newStage)
    {
        var (success, error) = await _opportunityService.ChangeStageAsync(User, id, newStage);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to update stage.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Opportunity moved to {newStage}.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var (success, error) = await _opportunityService.DeleteOpportunityAsync(User, id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to mark opportunity as lost.";
        }
        else
        {
            TempData["SuccessMessage"] = "Opportunity marked as Lost.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(int? selectedCustomerId = null, string? selectedUserId = null)
    {
        var customers = await _context.Customers
            .Where(c => c.Status == "Active")
            .Select(c => new { c.CustomerId, c.CustomerName })
            .ToListAsync();
        ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName", selectedCustomerId);

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
