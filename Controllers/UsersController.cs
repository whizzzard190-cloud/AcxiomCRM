using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _context.Users
            .Include(u => u.Manager)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                (u.Email != null && u.Email.Contains(term)));
        }

        int pageSize = 10;
        int totalRecords = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page < 1) page = 1;

        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = new List<UserItemViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var isLocked = await _userManager.IsLockedOutAsync(u);

            list.Add(new UserItemViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = roles.FirstOrDefault() ?? "None",
                ManagerName = u.Manager?.FullName,
                IsActive = u.IsActive,
                IsLockedOut = isLocked,
                CreatedDate = u.CreatedDate
            });
        }

        var model = new PagedList<UserItemViewModel>
        {
            Items = list,
            Pager = new PagerModel
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                SearchTerm = search,
                Controller = "Users",
                Action = "Index"
            }
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        await PopulateManagersDropdownAsync();
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateManagersDropdownAsync(model.ManagerId);
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "A user with this email address already exists.");
            await PopulateManagersDropdownAsync(model.ManagerId);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            ManagerId = !string.IsNullOrEmpty(model.ManagerId) ? model.ManagerId : null,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (await _roleManager.RoleExistsAsync(model.Role))
            {
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _auditService.LogAsync(
                action: "Create",
                module: "User",
                entityName: "ApplicationUser",
                recordId: user.Id,
                oldValue: null,
                newValue: new { user.FullName, user.Email, model.Role, user.ManagerId },
                result: "Success",
                userId: currentUserId);

            TempData["SuccessMessage"] = $"User '{user.FullName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        await PopulateManagersDropdownAsync(model.ManagerId);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isLocked = await _userManager.IsLockedOutAsync(user);

        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? "SalesExecutive",
            ManagerId = user.ManagerId,
            IsActive = user.IsActive,
            IsLockedOut = isLocked
        };

        await PopulateManagersDropdownAsync(user.ManagerId, user.Id);
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EditUserViewModel model)
    {
        if (id != model.Id)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            await PopulateManagersDropdownAsync(model.ManagerId, model.Id);
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var oldRoles = await _userManager.GetRolesAsync(user);
        var oldRole = oldRoles.FirstOrDefault() ?? "";

        var oldState = new
        {
            user.FullName,
            user.Email,
            Role = oldRole,
            user.ManagerId,
            user.IsActive
        };

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.ManagerId = !string.IsNullOrEmpty(model.ManagerId) ? model.ManagerId : null;
        user.IsActive = model.IsActive;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateManagersDropdownAsync(model.ManagerId, model.Id);
            return View(model);
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Role change check
        if (!string.Equals(oldRole, model.Role, StringComparison.OrdinalIgnoreCase))
        {
            if (oldRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, oldRoles);
            }
            if (await _roleManager.RoleExistsAsync(model.Role))
            {
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            // Golden Test G024: role change -> audit
            await _auditService.LogAsync(
                action: "RoleChange",
                module: "User",
                entityName: "ApplicationUser",
                recordId: user.Id,
                oldValue: new { Role = oldRole },
                newValue: new { Role = model.Role },
                result: "Success",
                userId: currentUserId);
        }

        await _auditService.LogAsync(
            action: "Update",
            module: "User",
            entityName: "ApplicationUser",
            recordId: user.Id,
            oldValue: oldState,
            newValue: new { user.FullName, user.Email, Role = model.Role, user.ManagerId, user.IsActive },
            result: "Success",
            userId: currentUserId);

        TempData["SuccessMessage"] = $"User '{user.FullName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        // Golden Test G036: Admin unlock of locked user -> works + audit
        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        if (result.Succeeded)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _auditService.LogAsync(
                action: "Unlock",
                module: "User",
                entityName: "ApplicationUser",
                recordId: user.Id,
                oldValue: new { Locked = true },
                newValue: new { Locked = false, Message = "Admin unlocked account" },
                result: "Success",
                userId: currentUserId);

            TempData["SuccessMessage"] = $"User '{user.FullName}' has been unlocked successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Unable to unlock user.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(new AdminResetPasswordViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? ""
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _auditService.LogAsync(
                action: "PasswordReset",
                module: "User",
                entityName: "ApplicationUser",
                recordId: user.Id,
                oldValue: null,
                newValue: new { Message = "Admin reset user password" },
                result: "Success",
                userId: currentUserId);

            TempData["SuccessMessage"] = $"Password for '{user.FullName}' was reset successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    private async Task PopulateManagersDropdownAsync(string? selectedManagerId = null, string? excludeUserId = null)
    {
        var query = _context.Users
            .Where(u => u.IsActive);

        if (!string.IsNullOrEmpty(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        var managers = await query
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();

        ViewBag.Managers = new SelectList(managers, "Id", "FullName", selectedManagerId);
    }
}
