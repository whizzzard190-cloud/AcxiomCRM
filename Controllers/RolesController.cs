using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public RolesController(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles.ToListAsync();
        var model = new List<RoleDetailsViewModel>();

        foreach (var r in roles)
        {
            var userCount = (await _userManager.GetUsersInRoleAsync(r.Name!)).Count;
            var perms = await _context.RolePermissions
                .Where(rp => rp.RoleId == r.Id)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission != null ? rp.Permission.Name : "")
                .ToListAsync();

            model.Add(new RoleDetailsViewModel
            {
                RoleId = r.Id,
                RoleName = r.Name ?? "",
                UserCount = userCount,
                Permissions = perms
            });
        }

        return View(model);
    }
}
