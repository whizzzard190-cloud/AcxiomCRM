using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly ApplicationDbContext _context;

    public CustomersController(ICustomerService customerService, ApplicationDbContext context)
    {
        _customerService = customerService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? sortBy, string? sortOrder, int page = 1)
    {
        var model = await _customerService.GetCustomersAsync(User, search, sortBy, sortOrder, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(User, id);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }
        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        return View(new Customer());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(customer.OwnerId);
            return View(customer);
        }

        var (success, error, created) = await _customerService.CreateCustomerAsync(User, customer);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateUsersDropdownAsync(customer.OwnerId);
            return View(customer);
        }

        TempData["SuccessMessage"] = $"Customer '{created!.CustomerName}' was created successfully.";
        return RedirectToAction(nameof(Details), new { id = created.CustomerId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(User, id);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Requested record was not found or access is denied.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateUsersDropdownAsync(customer.OwnerId);
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId)
        {
            TempData["ErrorMessage"] = "Requested record was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(customer.OwnerId);
            return View(customer);
        }

        var (success, error, updated) = await _customerService.UpdateCustomerAsync(User, customer);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "An error occurred.");
            await PopulateUsersDropdownAsync(customer.OwnerId);
            return View(customer);
        }

        TempData["SuccessMessage"] = $"Customer '{updated!.CustomerName}' was updated successfully.";
        return RedirectToAction(nameof(Details), new { id = updated.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var (success, error) = await _customerService.DeactivateCustomerAsync(User, id);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to deactivate customer.";
        }
        else
        {
            TempData["SuccessMessage"] = "Customer has been deactivated.";
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
