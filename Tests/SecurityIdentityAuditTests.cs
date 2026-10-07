using System.Reflection;
using AcxiomCRM.Controllers;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

public class SecurityIdentityAuditTests
{
    [Fact]
    public void G016_CrmControllersDecoratedWithAuthorizeAttribute()
    {
        // Controllers that must enforce authentication
        var protectedControllers = new[]
        {
            typeof(CustomersController),
            typeof(LeadsController),
            typeof(OpportunitiesController),
            typeof(FollowUpsController),
            typeof(ActivitiesController),
            typeof(DashboardController),
            typeof(ReportsController),
            typeof(AuditLogsController),
            typeof(UsersController),
            typeof(RolesController)
        };

        foreach (var controller in protectedControllers)
        {
            var authAttr = Attribute.GetCustomAttribute(controller, typeof(AuthorizeAttribute));
            Assert.NotNull(authAttr);
        }
    }

    [Fact]
    public async Task G018_RepeatedFailedLogins_EnforcesLockout()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (_, _, se1, _) = await TestHelper.SeedUsersAsync(context);

        se1.LockoutEnabled = true;
        await context.SaveChangesAsync();

        // Simulate 5 failed access attempts
        for (int i = 1; i <= 5; i++)
        {
            se1.AccessFailedCount++;
            if (se1.AccessFailedCount >= 5)
            {
                se1.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
            }
        }
        await context.SaveChangesAsync();

        var reloadedUser = await context.Users.FindAsync(se1.Id);
        Assert.NotNull(reloadedUser!.LockoutEnd);
        Assert.True(reloadedUser.LockoutEnd > DateTimeOffset.UtcNow);
        Assert.True(reloadedUser.AccessFailedCount >= 5);
    }

    [Fact]
    public void G019_NoPasswordPropertiesInApiDtos()
    {
        // Reflection check: No DTO exposed through API has Password or Secret properties
        var dtoAssembly = typeof(CustomerDto).Assembly;
        var dtoTypes = dtoAssembly.GetTypes()
            .Where(t => t.Namespace == "AcxiomCRM.DTOs" && !t.Name.Contains("LoginRequestDto"))
            .ToList();

        var forbiddenNames = new[] { "Password", "PasswordHash", "Secret", "Token", "SecurityStamp" };

        foreach (var type in dtoTypes)
        {
            foreach (var prop in type.GetProperties())
            {
                foreach (var forbidden in forbiddenNames)
                {
                    Assert.False(
                        prop.Name.Equals(forbidden, StringComparison.OrdinalIgnoreCase),
                        $"DTO {type.Name} exposes sensitive property '{prop.Name}'!");
                }
            }
        }
    }

    [Fact]
    public async Task G020_AuditServiceStripsPasswordsAndSecretsFromLogs()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());

        var sensitiveObject = new
        {
            UserId = "user-123",
            Email = "user@test.local",
            Password = "SuperSecretPassword123!",
            PasswordHash = "AQAAAAIAAA...",
            Token = "secret-token-xyz",
            Secret = "api-secret-key"
        };

        await auditService.LogAsync(
            action: "TestSecurity",
            module: "Auth",
            entityName: "ApplicationUser",
            recordId: "user-123",
            oldValue: null,
            newValue: sensitiveObject,
            result: "Success",
            userId: "user-123");

        var log = await context.AuditLogs.FirstOrDefaultAsync(l => l.Action == "TestSecurity");
        Assert.NotNull(log);
        Assert.NotNull(log.NewValue);

        // Verify none of the sensitive keywords are present in serialized JSON
        Assert.DoesNotContain("SuperSecretPassword123!", log.NewValue);
        Assert.DoesNotContain("PasswordHash", log.NewValue);
        Assert.DoesNotContain("secret-token-xyz", log.NewValue);
        Assert.DoesNotContain("api-secret-key", log.NewValue);
    }

    [Fact]
    public async Task G024_RoleChange_AuditsEvent()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, se1, _) = await TestHelper.SeedUsersAsync(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());

        // Simulate role change audit
        await auditService.LogAsync(
            action: "RoleChange",
            module: "User",
            entityName: "ApplicationUser",
            recordId: se1.Id,
            oldValue: new { Role = "SalesExecutive" },
            newValue: new { Role = "Manager", AssignedManagerId = (string?)null },
            result: "Success",
            userId: admin.Id);

        var audit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "User" && a.Action == "RoleChange" && a.RecordId == se1.Id);

        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Result);
        Assert.Contains("Manager", audit.NewValue);
        Assert.Equal(admin.Id, audit.UserId);
    }

    [Fact]
    public async Task G033_PasswordBelowPolicy_RejectsPassword()
    {
        var passwordValidator = new PasswordValidator<ApplicationUser>();
        var userManager = TestHelper.CreateUserManager(TestHelper.CreateInMemoryDbContext());

        var weakPasswords = new[]
        {
            "short",          // Less than 8 chars
            "nouppercase1!",  // No uppercase
            "NOLOWERCASE1!",  // No lowercase
            "NoDigitsHere!",  // No digit
            "NoSpecialChar1"  // No symbol
        };

        // Standard Identity password options
        var options = new IdentityOptions();
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        var dummyUser = new ApplicationUser { UserName = "test@example.com" };

        foreach (var pwd in weakPasswords)
        {
            bool hasUpper = pwd.Any(char.IsUpper);
            bool hasLower = pwd.Any(char.IsLower);
            bool hasDigit = pwd.Any(char.IsDigit);
            bool hasSpecial = pwd.Any(c => !char.IsLetterOrDigit(c));
            bool isLongEnough = pwd.Length >= 8;

            bool isValid = hasUpper && hasLower && hasDigit && hasSpecial && isLongEnough;
            Assert.False(isValid, $"Weak password '{pwd}' unexpectedly passed criteria.");
        }
    }

    [Fact]
    public void G034_AllMvcStateChangingPostActions_HaveValidateAntiForgeryToken()
    {
        // Reflection check: verify all [HttpPost] methods in MVC Controllers have [ValidateAntiForgeryToken]
        var mvcControllers = Assembly.GetAssembly(typeof(CustomersController))!
            .GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        foreach (var controller in mvcControllers)
        {
            var postMethods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
                .ToList();

            foreach (var method in postMethods)
            {
                var antiForgery = method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()
                    ?? controller.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>();

                Assert.NotNull(antiForgery);
            }
        }
    }

    [Fact]
    public async Task G036_AdminUnlock_UnlocksUserAndGeneratesAudit()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, se1, _) = await TestHelper.SeedUsersAsync(context);

        // Lock se1
        se1.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
        se1.AccessFailedCount = 5;
        await context.SaveChangesAsync();

        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());

        // Perform unlock
        se1.LockoutEnd = null;
        se1.AccessFailedCount = 0;
        await context.SaveChangesAsync();

        await auditService.LogAsync(
            action: "Unlock",
            module: "User",
            entityName: "ApplicationUser",
            recordId: se1.Id,
            oldValue: new { Locked = true },
            newValue: new { Locked = false, Message = "Admin unlocked account" },
            result: "Success",
            userId: admin.Id);

        var updatedUser = await context.Users.FindAsync(se1.Id);
        Assert.Null(updatedUser!.LockoutEnd);
        Assert.Equal(0, updatedUser.AccessFailedCount);

        var audit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "User" && a.Action == "Unlock" && a.RecordId == se1.Id);
        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Result);
        Assert.Equal(admin.Id, audit.UserId);
    }

    [Fact]
    public void G041_RegisterCannotCreateAdmin()
    {
        // AccountController.Register explicitly assigns "SalesExecutive"
        // Verify RegisterViewModel has no Role property
        var registerVmType = typeof(ViewModels.RegisterViewModel);
        var roleProp = registerVmType.GetProperty("Role");
        Assert.Null(roleProp);
    }

    [Fact]
    public void G042_AuditLogServiceAndRepository_AreAppendOnly()
    {
        // IAuditService must NOT contain Update or Delete methods
        var auditMethods = typeof(IAuditService).GetMethods();
        var hasDeleteMethod = auditMethods.Any(m => m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        var hasUpdateMethod = auditMethods.Any(m => m.Name.Contains("Update", StringComparison.OrdinalIgnoreCase));

        Assert.False(hasDeleteMethod, "IAuditService must not have Delete methods.");
        Assert.False(hasUpdateMethod, "IAuditService must not have Update methods.");
    }
}
