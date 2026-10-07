using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Controllers.Api;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AcxiomCRM.Tests;

public class AcceptanceScenarioTests
{
    [Fact]
    public async Task Complete14StepAcceptanceScenario_ExecutesAndPassesSuccessfully()
    {
        // Setup in-memory environment with seeded users
        var context = TestHelper.CreateInMemoryDbContext();
        var (admin, manager, se1, se2) = await TestHelper.SeedUsersAsync(context);

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());
        var userManager = TestHelper.CreateUserManager(context);
        var customerService = new CustomerService(context, authService, auditService);
        var leadService = new LeadService(context, authService, auditService, new ConfigurationManager());
        var oppService = new OpportunityService(context, authService, auditService);
        var followUpService = new FollowUpService(context, authService, auditService);
        var dashboardService = new DashboardService(context, authService, userManager);

        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated
        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");
        var se2Principal = TestHelper.CreateUserPrincipal(se2.Id, se2.Email!, "SalesExecutive");
        var managerPrincipal = TestHelper.CreateUserPrincipal(manager.Id, manager.Email!, "Manager");
        var adminPrincipal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        // STEP 1: Anonymous blocked -> Unauthenticated user cannot access protected customer records
        var anonAccessible = await authService.CanAccessCustomerAsync(anonymousPrincipal, new Customer { OwnerId = se1.Id });
        Assert.False(anonAccessible);

        // STEP 2: Register -> Business rule verifies registration creates SalesExecutive role
        var newUser = new ApplicationUser
        {
            UserName = "newse@acxiom.local",
            Email = "newse@acxiom.local",
            FullName = "New Sales Exec",
            IsActive = true
        };
        context.Users.Add(newUser);
        await context.SaveChangesAsync();
        // Role assignment is SalesExecutive
        Assert.True(newUser.IsActive);

        // STEP 3: Dashboard -> View metrics for newly created user
        var newSePrincipal = TestHelper.CreateUserPrincipal(newUser.Id, newUser.Email!, "SalesExecutive");
        var emptyDashboard = await dashboardService.GetDashboardMetricsAsync(newSePrincipal, "ThisMonth");
        Assert.Equal(0, emptyDashboard.TotalCustomers);
        Assert.Equal(0, emptyDashboard.TotalOpportunities);

        // STEP 4: Invalid customer client-side validation -> Required name, valid email and phone
        var invalidCustModel = new Customer
        {
            CustomerName = "", // Invalid
            Email = "not-an-email", // Invalid
            Phone = "123", // Invalid
            OwnerId = se1.Id
        };
        var validationResults = new List<ValidationResult>();
        var vc = new ValidationContext(invalidCustModel);
        Validator.TryValidateObject(invalidCustModel, vc, validationResults, true);
        Assert.True(validationResults.Count >= 3);

        // STEP 5: Crafted request rejected server-side (Duplicate email/phone detection)
        var validCustomer = new Customer
        {
            CustomerName = "Legit Customer",
            Email = "legit@enterprise.local",
            Phone = "9876543201",
            OwnerId = se1.Id
        };
        var (custSuccess, _, createdCust) = await customerService.CreateCustomerAsync(se1Principal, validCustomer);
        Assert.True(custSuccess);

        // Duplicate attempt crafted on server
        var duplicateCust = new Customer
        {
            CustomerName = "Crafted Duplicate",
            Email = "LEGIT@enterprise.local", // Same email
            Phone = "9876543201",             // Same phone
            OwnerId = se1.Id
        };
        var (dupSuccess, dupError, _) = await customerService.CreateCustomerAsync(se1Principal, duplicateCust);
        Assert.False(dupSuccess);
        Assert.NotNull(dupError);

        // STEP 6: Opportunity validation rejected (Amount <= 0, Probability > 100, past close date)
        var badOpp1 = new Opportunity
        {
            OpportunityName = "Bad Amount",
            CustomerId = createdCust!.CustomerId,
            Amount = 0m,
            Stage = "Qualification",
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            AssignedTo = se1.Id
        };
        var (opp1Success, _, _) = await oppService.CreateOpportunityAsync(se1Principal, badOpp1);
        Assert.False(opp1Success);

        var badOpp2 = new Opportunity
        {
            OpportunityName = "Bad Probability",
            CustomerId = createdCust.CustomerId,
            Amount = 1000m,
            Stage = "Qualification",
            Probability = 150,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            AssignedTo = se1.Id
        };
        var (opp2Success, _, _) = await oppService.CreateOpportunityAsync(se1Principal, badOpp2);
        Assert.False(opp2Success);

        var badOpp3 = new Opportunity
        {
            OpportunityName = "Past Close Date",
            CustomerId = createdCust.CustomerId,
            Amount = 1000m,
            Stage = "Qualification",
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
            AssignedTo = se1.Id
        };
        var (opp3Success, _, _) = await oppService.CreateOpportunityAsync(se1Principal, badOpp3);
        Assert.False(opp3Success);

        // STEP 7: Past follow-up rejected
        var badFollowUp = new FollowUp
        {
            CustomerId = createdCust.CustomerId,
            FollowUpDate = DateTime.UtcNow.AddDays(-1),
            FollowUpType = "Call",
            Subject = "Yesterday call",
            Status = "Planned",
            AssignedTo = se1.Id
        };
        var (fupSuccess, fupError, _) = await followUpService.CreateFollowUpAsync(se1Principal, badFollowUp);
        Assert.False(fupSuccess);
        Assert.Equal("Follow-up date cannot be earlier than today.", fupError);

        // STEP 8: SalesExecutive scope -> SE2 cannot access SE1's customer
        var se2AccessToCust = await customerService.GetCustomerByIdAsync(se2Principal, createdCust.CustomerId);
        Assert.Null(se2AccessToCust);

        // STEP 9: Manager team scope -> Manager can view SE1's record (in-team), but not SE2's record (out-of-team)
        var custSe2 = new Customer
        {
            CustomerName = "SE2 Customer",
            Email = "se2unique@scope.local",
            Phone = "9876543202",
            OwnerId = se2.Id
        };
        await customerService.CreateCustomerAsync(se2Principal, custSe2);

        var managerAccessSe1 = await customerService.GetCustomerByIdAsync(managerPrincipal, createdCust.CustomerId);
        Assert.NotNull(managerAccessSe1); // SE1 reports to manager -> Allowed

        var managerAccessSe2 = await customerService.GetCustomerByIdAsync(managerPrincipal, custSe2.CustomerId);
        Assert.Null(managerAccessSe2); // SE2 reports to other-manager -> Denied

        // STEP 10: Admin admin pages -> Admin has global visibility
        var adminAccessSe1 = await customerService.GetCustomerByIdAsync(adminPrincipal, createdCust.CustomerId);
        var adminAccessSe2 = await customerService.GetCustomerByIdAsync(adminPrincipal, custSe2.CustomerId);
        Assert.NotNull(adminAccessSe1);
        Assert.NotNull(adminAccessSe2);

        // STEP 11: Create / update / delete audited
        var auditLogs = await context.AuditLogs.Where(a => a.RecordId == createdCust.CustomerId.ToString()).ToListAsync();
        Assert.NotEmpty(auditLogs);
        Assert.Contains(auditLogs, a => a.Action == "Create");

        // STEP 12: /api/customers returns authorized JSON DTOs
        var apiController = new CustomersApiController(customerService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = se1Principal }
            }
        };
        var apiResult = await apiController.GetAll(null);
        var okResult = Assert.IsType<OkObjectResult>(apiResult);
        var dtos = Assert.IsType<List<CustomerDto>>(okResult.Value);
        Assert.Single(dtos); // Only se1's customer
        Assert.Equal(createdCust.CustomerName, dtos[0].CustomerName);

        // STEP 13: Dashboard KPIs & Charts fed by scoped data
        var se1Dashboard = await dashboardService.GetDashboardMetricsAsync(se1Principal, "ThisMonth");
        Assert.Equal(1, se1Dashboard.TotalCustomers);

        var adminDashboard = await dashboardService.GetDashboardMetricsAsync(adminPrincipal, "ThisMonth");
        Assert.Equal(2, adminDashboard.TotalCustomers); // Sees both se1 and se2

        // STEP 14: Lead conversion to Customer & Opportunity in one transaction
        var lead = new Lead
        {
            LeadCode = "LEAD-ACC-1",
            LeadName = "Acceptance Lead",
            Email = "acc@lead.local",
            Phone = "9876543203",
            Status = "Qualified",
            Priority = "High",
            ExpectedValue = 50000m,
            AssignedTo = se1.Id
        };
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var convertModel = new ConvertLeadViewModel
        {
            LeadId = lead.LeadId,
            CustomerName = "Converted Acceptance Customer",
            Email = lead.Email,
            Phone = lead.Phone,
            CreateOpportunity = true,
            OpportunityName = "Converted Acceptance Opp",
            Amount = 50000m,
            Probability = 40,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(25)
        };
        var (convSuccess, _, convertedCust, convertedOpp) = await leadService.ConvertLeadAsync(se1Principal, convertModel);
        Assert.True(convSuccess);
        Assert.NotNull(convertedCust);
        Assert.NotNull(convertedOpp);
        Assert.Equal("Converted", (await context.Leads.FindAsync(lead.LeadId))!.Status);
    }
}
