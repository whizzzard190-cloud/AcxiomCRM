using AcxiomCRM.Authorization;
using AcxiomCRM.Controllers.Api;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

public class ScopeAndDashboardTests
{
    private static async Task<(ApplicationDbContext context, ApplicationUser admin, ApplicationUser manager, ApplicationUser se1, ApplicationUser se2)> SetupDatabaseAsync()
    {
        var context = TestHelper.CreateInMemoryDbContext();
        var (admin, manager, se1, se2) = await TestHelper.SeedUsersAsync(context);

        // Seed customer for SE1 (Team Manager-1)
        var cust1 = new Customer
        {
            CustomerName = "SE1 Customer",
            Email = "se1cust@scope.local",
            Phone = "9876543271",
            OwnerId = se1.Id,
            CreatedDate = DateTime.UtcNow
        };

        // Seed customer for SE2 (Different Manager / out of team)
        var cust2 = new Customer
        {
            CustomerName = "SE2 Customer",
            Email = "se2cust@scope.local",
            Phone = "9876543272",
            OwnerId = se2.Id,
            CreatedDate = DateTime.UtcNow
        };

        // Seed lead for SE1
        var lead1 = new Lead
        {
            LeadCode = "LEAD-SCOPE-1",
            LeadName = "SE1 Lead",
            Email = "se1lead@scope.local",
            Phone = "9876543273",
            Status = "New",
            Priority = "Medium",
            ExpectedValue = 5000,
            AssignedTo = se1.Id,
            CreatedDate = DateTime.UtcNow
        };

        // Seed lead for SE2
        var lead2 = new Lead
        {
            LeadCode = "LEAD-SCOPE-2",
            LeadName = "SE2 Lead",
            Email = "se2lead@scope.local",
            Phone = "9876543274",
            Status = "Contacted",
            Priority = "High",
            ExpectedValue = 10000,
            AssignedTo = se2.Id,
            CreatedDate = DateTime.UtcNow
        };

        context.Customers.AddRange(cust1, cust2);
        context.Leads.AddRange(lead1, lead2);
        await context.SaveChangesAsync();

        return (context, admin, manager, se1, se2);
    }

    [Fact]
    public async Task G014_SalesExecutiveReadsAnotherSalesExecutivesCustomer_DeniesAccess()
    {
        var (context, _, _, se1, se2) = await SetupDatabaseAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");
        var custSe2 = await context.Customers.FirstAsync(c => c.OwnerId == se2.Id);

        // Check CanAccessCustomerAsync
        var canAccess = await authService.CanAccessCustomerAsync(se1Principal, custSe2);
        Assert.False(canAccess);

        // Through CustomerService
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(se1Principal));
        var custService = new CustomerService(context, authService, auditService);
        var result = await custService.GetCustomerByIdAsync(se1Principal, custSe2.CustomerId);
        Assert.Null(result); // Denied access returns null
    }

    [Fact]
    public async Task G037_ManagerReadsOutOfTeamRecord_DeniesAccess()
    {
        var (context, _, manager, _, se2) = await SetupDatabaseAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);

        var managerPrincipal = TestHelper.CreateUserPrincipal(manager.Id, manager.Email!, "Manager");
        var custSe2 = await context.Customers.FirstAsync(c => c.OwnerId == se2.Id);

        // SE2 reports to 'other-manager-99', NOT 'manager-1'
        var canAccess = await authService.CanAccessCustomerAsync(managerPrincipal, custSe2);
        Assert.False(canAccess);

        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(managerPrincipal));
        var custService = new CustomerService(context, authService, auditService);
        var result = await custService.GetCustomerByIdAsync(managerPrincipal, custSe2.CustomerId);
        Assert.Null(result);
    }

    [Fact]
    public async Task G030_DashboardAsSalesExecutive_ShowsOwnDataOnly()
    {
        var (context, _, _, se1, _) = await SetupDatabaseAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var userManager = TestHelper.CreateUserManager(context);
        var dashboardService = new DashboardService(context, authService, userManager);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");
        var metrics = await dashboardService.GetDashboardMetricsAsync(se1Principal, "ThisMonth");

        // SE1 owns exactly 1 customer and 1 lead
        Assert.Equal(1, metrics.TotalCustomers);
        Assert.Equal(1, metrics.TotalLeads);
    }

    [Fact]
    public async Task G031_DashboardAsManager_ShowsTeamData()
    {
        var (context, _, manager, _, _) = await SetupDatabaseAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var userManager = TestHelper.CreateUserManager(context);
        var dashboardService = new DashboardService(context, authService, userManager);

        var managerPrincipal = TestHelper.CreateUserPrincipal(manager.Id, manager.Email!, "Manager");
        var metrics = await dashboardService.GetDashboardMetricsAsync(managerPrincipal, "ThisMonth");

        // Manager's team consists of SE1 (who owns 1 customer and 1 lead). SE2 is out-of-team.
        Assert.Equal(1, metrics.TotalCustomers);
        Assert.Equal(1, metrics.TotalLeads);
    }

    [Fact]
    public async Task G032_DashboardAsAdmin_ShowsAllData()
    {
        var (context, admin, _, _, _) = await SetupDatabaseAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var userManager = TestHelper.CreateUserManager(context);
        var dashboardService = new DashboardService(context, authService, userManager);

        var adminPrincipal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");
        var metrics = await dashboardService.GetDashboardMetricsAsync(adminPrincipal, "ThisMonth");

        // Admin sees all: 2 customers and 2 leads
        Assert.Equal(2, metrics.TotalCustomers);
        Assert.Equal(2, metrics.TotalLeads);
    }

    [Fact]
    public void G017_G029_ApiControllersDecoratedWithAuthorizeAttribute()
    {
        // Reflection check that API controllers enforce [Authorize]
        var apiControllers = new[]
        {
            typeof(CustomersApiController),
            typeof(LeadsApiController),
            typeof(OpportunitiesApiController),
            typeof(FollowUpsApiController),
            typeof(ReportsApiController)
        };

        foreach (var controller in apiControllers)
        {
            var authorizeAttr = Attribute.GetCustomAttribute(controller, typeof(AuthorizeAttribute));
            Assert.NotNull(authorizeAttr);
        }
    }
}
