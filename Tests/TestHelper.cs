using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

public static class TestHelper
{
    public static ApplicationDbContext CreateInMemoryDbContext(string dbName = "")
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(string.IsNullOrEmpty(dbName) ? Guid.NewGuid().ToString() : dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    public static Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context)
    {
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser>(context);
        var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ApplicationUser>();
        return new Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>(
            userStore,
            null!,
            passwordHasher,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }

    public static ResourceAuthorizationService CreateResourceAuthorizationService(ApplicationDbContext context)
    {
        return new ResourceAuthorizationService(context, CreateUserManager(context));
    }

    public static ClaimsPrincipal CreateUserPrincipal(string userId, string email, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    public static IHttpContextAccessor CreateMockHttpContextAccessor(ClaimsPrincipal? principal = null)
    {
        var httpContext = new DefaultHttpContext();
        if (principal != null)
        {
            httpContext.User = principal;
        }
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    public static async Task<(ApplicationUser admin, ApplicationUser manager, ApplicationUser se1, ApplicationUser se2)> SeedUsersAsync(ApplicationDbContext context)
    {
        var admin = new ApplicationUser
        {
            Id = "admin-1",
            UserName = "admin@acxiomcrm.local",
            Email = "admin@acxiomcrm.local",
            FullName = "Admin User",
            IsActive = true
        };

        var manager = new ApplicationUser
        {
            Id = "manager-1",
            UserName = "manager@acxiomcrm.local",
            Email = "manager@acxiomcrm.local",
            FullName = "Manager User",
            IsActive = true
        };

        var se1 = new ApplicationUser
        {
            Id = "se1-1",
            UserName = "se1@acxiomcrm.local",
            Email = "se1@acxiomcrm.local",
            FullName = "Sales Exec 1",
            ManagerId = "manager-1",
            IsActive = true
        };

        var se2 = new ApplicationUser
        {
            Id = "se2-1",
            UserName = "se2@acxiomcrm.local",
            Email = "se2@acxiomcrm.local",
            FullName = "Sales Exec 2",
            ManagerId = "other-manager-99", // Different manager for testing cross-team scope!
            IsActive = true
        };

        context.Users.AddRange(admin, manager, se1, se2);
        await context.SaveChangesAsync();
        return (admin, manager, se1, se2);
    }
}
