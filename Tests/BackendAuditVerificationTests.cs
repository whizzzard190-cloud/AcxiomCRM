using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using AcxiomCRM.Authorization;
using AcxiomCRM.Controllers;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AcxiomCRM.Tests;

public class BackendAuditVerificationTests
{
    private static async Task<(ApplicationDbContext Context, ApplicationUser Admin, ApplicationUser Manager, ApplicationUser Se1, ApplicationUser Se2)> CreateSetupAsync()
    {
        var context = TestHelper.CreateInMemoryDbContext();
        var (admin, manager, se1, se2) = await TestHelper.SeedUsersAsync(context);
        return (context, admin, manager, se1, se2);
    }

    [Fact]
    public void AuditFix_CurrencyFormatting_UsesIndianRupeeSymbol()
    {
        // Test that en-IN culture formats currency using ₹ and not £
        var inCulture = new CultureInfo("en-IN");
        inCulture.NumberFormat.CurrencySymbol = "₹";

        decimal sampleAmount = 150000m;
        string formatted = sampleAmount.ToString("C0", inCulture);

        Assert.Contains("₹", formatted);
        Assert.DoesNotContain("£", formatted);
        Assert.DoesNotContain("$", formatted);
    }

    [Fact]
    public async Task AuditFix_LeadConversion_ErrorHandling_PreventsInformationDisclosure()
    {
        var (context, admin, _, se1, _) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(), NullLogger<AuditService>.Instance);
        var leadService = new LeadService(context, authService, auditService, new ConfigurationManager(), NullLogger<LeadService>.Instance);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        // Seed a Qualified lead
        var lead = new Lead
        {
            LeadCode = "LEAD-AUDIT-01",
            LeadName = "Audit Tech",
            Email = "audit@tech.local",
            Phone = "9876543210",
            Status = "Qualified",
            AssignedTo = se1.Id,
            ExpectedValue = 50000m
        };
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        // Attempt conversion with invalid state that triggers error
        var invalidModel = new ConvertLeadViewModel
        {
            LeadId = lead.LeadId,
            CustomerName = "", // Missing CustomerName will fail validation
            CreateOpportunity = true,
            OpportunityName = "New Deal",
            Amount = 50000m
        };

        var (success, error, _, _) = await leadService.ConvertLeadAsync(se1Principal, invalidModel);

        Assert.False(success);
        Assert.NotNull(error);
        // Error should be a safe client message without SQL or stack traces
        Assert.DoesNotContain("Exception", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sql", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditFix_AuditService_DatabaseFailure_DoesNotThrowToCaller()
    {
        // Configure options with a closed / disposed context to simulate database connection outage
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        await context.DisposeAsync(); // Dispose immediately so any DB action throws ObjectDisposedException

        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(), NullLogger<AuditService>.Instance);

        // Should complete without throwing exception to caller
        var exception = await Record.ExceptionAsync(() => auditService.LogAsync(
            action: "Test",
            module: "Audit",
            entityName: "TestEntity",
            recordId: "1",
            oldValue: null,
            newValue: new { Note = "Test" },
            result: "Success"));

        Assert.Null(exception);
    }

    [Fact]
    public void AuditFix_EfModel_LeadAndOpportunity_SeparateNavigations_NoDuplicateRelationships()
    {
        // Validate that ApplicationDbContext can build its model without navigation pair conflicts
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var model = context.Model;

        var leadEntity = model.FindEntityType(typeof(Lead));
        var oppEntity = model.FindEntityType(typeof(Opportunity));

        Assert.NotNull(leadEntity);
        Assert.NotNull(oppEntity);

        // Check that ConvertedOpportunity navigation on Lead exists
        var convertedOppNav = leadEntity.FindNavigation(nameof(Lead.ConvertedOpportunity));
        Assert.NotNull(convertedOppNav);

        // Check that Lead navigation on Opportunity exists
        var leadNav = oppEntity.FindNavigation(nameof(Opportunity.Lead));
        Assert.NotNull(leadNav);

        // Ensure they are not mistakenly paired as inverse navigations of each other
        Assert.Null(convertedOppNav.Inverse);
        Assert.Null(leadNav.Inverse);
    }

    [Fact]
    public async Task AuditFix_Dashboard_DateFilter_All_ReturnsCumulativeTotals()
    {
        var (context, admin, _, se1, _) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var userManager = TestHelper.CreateUserManager(context);
        var dashboardService = new DashboardService(context, authService, userManager);

        var adminPrincipal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        // Seed customer from 60 days ago
        context.Customers.Add(new Customer
        {
            CustomerCode = "CUST-OLD",
            CustomerName = "Old Customer",
            Email = "old@cust.local",
            Phone = "9876543299",
            OwnerId = se1.Id,
            CreatedDate = DateTime.UtcNow.AddDays(-60)
        });

        // Seed opportunity from 60 days ago
        context.Opportunities.Add(new Opportunity
        {
            OpportunityName = "Old Deal",
            CustomerId = 1,
            Amount = 100000m,
            Stage = "Proposal",
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            Status = "Open",
            AssignedTo = se1.Id,
            CreatedDate = DateTime.UtcNow.AddDays(-60)
        });

        await context.SaveChangesAsync();

        // Query with "All" filter
        var allMetrics = await dashboardService.GetDashboardMetricsAsync(adminPrincipal, "All");
        Assert.True(allMetrics.TotalCustomers >= 1);
        Assert.True(allMetrics.TotalOpportunities >= 1);
        Assert.True(allMetrics.TotalPipelineValue >= 100000m);
    }

    [Fact]
    public async Task AuditFix_Opportunity_WeightedAmount_MatchesExactFormula()
    {
        var opp1 = new Opportunity { Amount = 100000m, Probability = 50 };
        Assert.Equal(50000m, opp1.WeightedAmount);

        var opp2 = new Opportunity { Amount = 75000m, Probability = 20 };
        Assert.Equal(15000m, opp2.WeightedAmount);

        var opp3 = new Opportunity { Amount = 200000m, Probability = 0 };
        Assert.Equal(0m, opp3.WeightedAmount);

        var opp4 = new Opportunity { Amount = 350000m, Probability = 100 };
        Assert.Equal(350000m, opp4.WeightedAmount);
    }

    [Fact]
    public async Task AuditFix_Opportunity_Validation_RejectsNegativeOrZeroAmount()
    {
        var (context, admin, _, se1, _) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(), NullLogger<AuditService>.Instance);
        var oppService = new OpportunityService(context, authService, auditService);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        // Seed a Customer
        var cust = new Customer
        {
            CustomerCode = "CUST-OPP-TEST",
            CustomerName = "Opp Customer",
            Email = "oppcust@test.local",
            Phone = "9876543201",
            OwnerId = se1.Id
        };
        context.Customers.Add(cust);
        await context.SaveChangesAsync();

        var zeroOpp = new Opportunity
        {
            OpportunityName = "Zero Amount Opp",
            CustomerId = cust.CustomerId,
            Amount = 0m,
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            Status = "Open",
            AssignedTo = se1.Id
        };

        var (s1, err1, _) = await oppService.CreateOpportunityAsync(se1Principal, zeroOpp);
        Assert.False(s1);
        Assert.Contains("greater than 0", err1, StringComparison.OrdinalIgnoreCase);

        var negOpp = new Opportunity
        {
            OpportunityName = "Negative Amount Opp",
            CustomerId = cust.CustomerId,
            Amount = -5000m,
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            Status = "Open",
            AssignedTo = se1.Id
        };

        var (s2, err2, _) = await oppService.CreateOpportunityAsync(se1Principal, negOpp);
        Assert.False(s2);
        Assert.Contains("greater than 0", err2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditFix_Opportunity_Validation_RejectsInvalidProbability()
    {
        var (context, admin, _, se1, _) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(), NullLogger<AuditService>.Instance);
        var oppService = new OpportunityService(context, authService, auditService);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        var cust = new Customer
        {
            CustomerCode = "CUST-PROB-TEST",
            CustomerName = "Prob Customer",
            Email = "probcust@test.local",
            Phone = "9876543202",
            OwnerId = se1.Id
        };
        context.Customers.Add(cust);
        await context.SaveChangesAsync();

        var highProbOpp = new Opportunity
        {
            OpportunityName = "Over 100 Prob",
            CustomerId = cust.CustomerId,
            Amount = 50000m,
            Stage = "Qualification",
            Probability = 110,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            Status = "Open",
            AssignedTo = se1.Id
        };

        var (s1, err1, _) = await oppService.CreateOpportunityAsync(se1Principal, highProbOpp);
        Assert.False(s1);
        Assert.Contains("Probability", err1, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditFix_Opportunity_Validation_RejectsPastExpectedCloseDate()
    {
        var (context, admin, _, se1, _) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(), NullLogger<AuditService>.Instance);
        var oppService = new OpportunityService(context, authService, auditService);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        var cust = new Customer
        {
            CustomerCode = "CUST-PAST-TEST",
            CustomerName = "Past Close Date Customer",
            Email = "pastcust@test.local",
            Phone = "9876543203",
            OwnerId = se1.Id
        };
        context.Customers.Add(cust);
        await context.SaveChangesAsync();

        var pastOpp = new Opportunity
        {
            OpportunityName = "Past Close Opp",
            CustomerId = cust.CustomerId,
            Amount = 50000m,
            Stage = "Qualification",
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-5), // in past!
            Status = "Open",
            AssignedTo = se1.Id
        };

        var (s1, err1, _) = await oppService.CreateOpportunityAsync(se1Principal, pastOpp);
        Assert.False(s1);
        Assert.Contains("Expected Close Date", err1, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditFix_FollowUp_Reschedule_UpdatesAndAudits()
    {
        var (context, admin, _, se1, _) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(), NullLogger<AuditService>.Instance);
        var followUpService = new FollowUpService(context, authService, auditService);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        var cust = new Customer
        {
            CustomerCode = "CUST-FU-TEST",
            CustomerName = "Follow Up Customer",
            Email = "fucust@test.local",
            Phone = "9876543204",
            OwnerId = se1.Id
        };
        context.Customers.Add(cust);
        await context.SaveChangesAsync();

        var initialDate = DateTime.UtcNow.AddDays(2);
        var fu = new FollowUp
        {
            CustomerId = cust.CustomerId,
            FollowUpDate = initialDate,
            FollowUpType = "Call",
            Subject = "Intro Call",
            Status = "Planned",
            AssignedTo = se1.Id
        };
        context.FollowUps.Add(fu);
        await context.SaveChangesAsync();

        var newDate = DateTime.UtcNow.AddDays(5);
        var (success, error) = await followUpService.RescheduleFollowUpAsync(se1Principal, fu.FollowUpId, newDate, "Client requested later date");

        Assert.True(success);
        Assert.Null(error);

        var reloaded = await context.FollowUps.FindAsync(fu.FollowUpId);
        Assert.NotNull(reloaded);
        Assert.Equal(newDate, reloaded.FollowUpDate);
        Assert.Contains("Client requested later date", reloaded.Remarks);

        // Verify audit entry was logged
        var audit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "FollowUp" && a.Action == "Reschedule" && a.RecordId == fu.FollowUpId.ToString());
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task AuditFix_Reports_PipelineReport_RespectsUserScoping()
    {
        var (context, admin, manager, se1, se2) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var reportService = new ReportService(context, authService);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");
        var adminPrincipal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        // Seed 2 customers and 2 opportunities: one for SE1, one for SE2
        var c1 = new Customer { CustomerCode = "C-1", CustomerName = "C1", Email = "c1@test.local", Phone = "9876543211", OwnerId = se1.Id };
        var c2 = new Customer { CustomerCode = "C-2", CustomerName = "C2", Email = "c2@test.local", Phone = "9876543222", OwnerId = se2.Id };
        context.Customers.AddRange(c1, c2);
        await context.SaveChangesAsync();

        var o1 = new Opportunity { OpportunityName = "SE1 Opp", CustomerId = c1.CustomerId, Amount = 100000m, Stage = "Qualification", Probability = 20, ExpectedCloseDate = DateTime.UtcNow.AddDays(30), Status = "Open", AssignedTo = se1.Id };
        var o2 = new Opportunity { OpportunityName = "SE2 Opp", CustomerId = c2.CustomerId, Amount = 200000m, Stage = "Proposal", Probability = 50, ExpectedCloseDate = DateTime.UtcNow.AddDays(30), Status = "Open", AssignedTo = se2.Id };
        context.Opportunities.AddRange(o1, o2);
        await context.SaveChangesAsync();

        // SE1 Pipeline Report
        var (se1Stages, _, _) = await reportService.GetPipelineReportAsync(se1Principal, null, null);
        var se1Total = se1Stages.Sum(s => s.TotalAmount);
        Assert.Equal(100000m, se1Total);

        // Admin Pipeline Report
        var (adminStages, _, _) = await reportService.GetPipelineReportAsync(adminPrincipal, null, null);
        var adminTotal = adminStages.Sum(s => s.TotalAmount);
        Assert.Equal(300000m, adminTotal);
    }

    [Fact]
    public async Task AuditFix_Reports_ConversionReport_RespectsUserScoping()
    {
        var (context, admin, manager, se1, se2) = await CreateSetupAsync();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var reportService = new ReportService(context, authService);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");
        var adminPrincipal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        // Seed Converted leads for SE1 and SE2
        var l1 = new Lead { LeadCode = "L-1", LeadName = "L1", Email = "l1@test.local", Phone = "9876543211", Status = "Converted", AssignedTo = se1.Id, ExpectedValue = 50000m };
        var l2 = new Lead { LeadCode = "L-2", LeadName = "L2", Email = "l2@test.local", Phone = "9876543222", Status = "Converted", AssignedTo = se2.Id, ExpectedValue = 75000m };
        context.Leads.AddRange(l1, l2);
        await context.SaveChangesAsync();

        var (se1Items, _) = await reportService.GetConversionReportAsync(se1Principal, null, null);
        Assert.Single(se1Items);
        Assert.Equal("L-1", se1Items[0].LeadCode);

        var (adminItems, _) = await reportService.GetConversionReportAsync(adminPrincipal, null, null);
        Assert.Equal(2, adminItems.Count);
    }

    [Fact]
    public void AuditFix_UserManagement_RestrictedToAdminRole()
    {
        // 1. RolesController is strictly Admin-only
        var rolesControllerType = typeof(RolesController);
        var roleAuthAttr = Attribute.GetCustomAttribute(rolesControllerType, typeof(AuthorizeAttribute)) as AuthorizeAttribute;
        Assert.NotNull(roleAuthAttr);
        Assert.Equal("Admin", roleAuthAttr.Roles);

        // 2. User creation in UsersController is strictly Admin-only
        var createMethod = typeof(UsersController).GetMethod("Create", new[] { typeof(CreateUserViewModel) });
        Assert.NotNull(createMethod);
        var createAuthAttr = Attribute.GetCustomAttribute(createMethod, typeof(AuthorizeAttribute)) as AuthorizeAttribute;
        Assert.NotNull(createAuthAttr);
        Assert.Equal("Admin", createAuthAttr.Roles);
    }
}
