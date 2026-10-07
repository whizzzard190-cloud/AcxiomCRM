using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AcxiomCRM.Tests;

public class WorkflowAndAuditingTests
{
    [Fact]
    public async Task G015_SalesExecutiveEditsAnotherSalesExecutivesOpportunity_DeniesAccess()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (_, _, se1, se2) = await TestHelper.SeedUsersAsync(context);

        var se1Principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");
        var se2Principal = TestHelper.CreateUserPrincipal(se2.Id, se2.Email!, "SalesExecutive");

        var customer = new Customer
        {
            CustomerName = "SE2 Customer",
            Email = "se2cust@acxiom.local",
            Phone = "9876543250",
            OwnerId = se2.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var opp = new Opportunity
        {
            OpportunityName = "SE2 Opportunity",
            CustomerId = customer.CustomerId,
            Amount = 10000m,
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
            AssignedTo = se2.Id // Owned by SE2
        };
        context.Opportunities.Add(opp);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(se1Principal));
        var oppService = new OpportunityService(context, authService, auditService);

        // SE1 tries to edit SE2's opportunity
        opp.OpportunityName = "Tampered Opportunity by SE1";
        var (success, error, _) = await oppService.UpdateOpportunityAsync(se1Principal, opp);

        Assert.False(success);
        Assert.Equal("You are not authorized to perform this action.", error);
    }

    [Fact]
    public async Task G025_LeadConversion_CreatesCustomerOpportunityAndAuditLog()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, se1, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        var lead = new Lead
        {
            LeadCode = "LEAD-CONVERT-1",
            LeadName = "Qualified Lead",
            Email = "qualified@lead.local",
            Phone = "9876543255",
            CompanyName = "Convert Inc",
            Status = "Qualified",
            Priority = "High",
            ExpectedValue = 75000m,
            AssignedTo = se1.Id
        };
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var leadService = new LeadService(context, authService, auditService, new ConfigurationManager());

        var model = new ConvertLeadViewModel
        {
            LeadId = lead.LeadId,
            CustomerName = "Convert Inc Customer",
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            CreateOpportunity = true,
            OpportunityName = "Converted Big Deal",
            Amount = lead.ExpectedValue,
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(20)
        };

        var (success, error, customer, opp) = await leadService.ConvertLeadAsync(principal, model);

        Assert.True(success, error);
        Assert.NotNull(customer);
        Assert.NotNull(opp);

        // Verify Lead status was set to Converted
        var updatedLead = await context.Leads.FindAsync(lead.LeadId);
        Assert.Equal("Converted", updatedLead!.Status);
        Assert.Equal(customer.CustomerId, updatedLead.ConvertedCustomerId);
        Assert.Equal(opp.OpportunityId, updatedLead.ConvertedOpportunityId);

        // Verify Audit Log was generated
        var audit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "Lead" && a.Action == "Convert" && a.RecordId == lead.LeadId.ToString());
        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Result);
        Assert.Contains(customer.CustomerName, audit.NewValue);
    }

    [Fact]
    public async Task G026_FollowUpComplete_GeneratesAuditLog()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, se1, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        var customer = new Customer
        {
            CustomerName = "FollowUp Customer",
            Email = "fup@acxiom.local",
            Phone = "9876543260",
            OwnerId = se1.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var followUp = new FollowUp
        {
            CustomerId = customer.CustomerId,
            FollowUpDate = DateTime.UtcNow.AddDays(1),
            FollowUpType = "Call",
            Subject = "Closing pitch",
            Status = "Planned",
            AssignedTo = se1.Id
        };
        context.FollowUps.Add(followUp);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var followUpService = new FollowUpService(context, authService, auditService);

        var (success, error) = await followUpService.CompleteFollowUpAsync(principal, followUp.FollowUpId, "Customer agreed to proposal.");
        Assert.True(success, error);

        var updated = await context.FollowUps.FindAsync(followUp.FollowUpId);
        Assert.Equal("Completed", updated!.Status);

        var audit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "FollowUp" && a.Action == "Complete" && a.RecordId == followUp.FollowUpId.ToString());
        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Result);
        Assert.Contains("Completed", audit.NewValue);
    }

    [Fact]
    public async Task G027_FollowUpReschedule_GeneratesAuditLog()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, se1, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(se1.Id, se1.Email!, "SalesExecutive");

        var customer = new Customer
        {
            CustomerName = "Reschedule Customer",
            Email = "resched@acxiom.local",
            Phone = "9876543265",
            OwnerId = se1.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var followUp = new FollowUp
        {
            CustomerId = customer.CustomerId,
            FollowUpDate = DateTime.UtcNow.AddDays(1),
            FollowUpType = "Meeting",
            Subject = "Demo Meeting",
            Status = "Planned",
            AssignedTo = se1.Id
        };
        context.FollowUps.Add(followUp);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var followUpService = new FollowUpService(context, authService, auditService);

        var newDate = DateTime.UtcNow.AddDays(5);
        var (success, error) = await followUpService.RescheduleFollowUpAsync(principal, followUp.FollowUpId, newDate, "Client was traveling.");
        Assert.True(success, error);

        var updated = await context.FollowUps.FindAsync(followUp.FollowUpId);
        Assert.Equal(newDate, updated!.FollowUpDate);

        var audit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "FollowUp" && a.Action == "Reschedule" && a.RecordId == followUp.FollowUpId.ToString());
        Assert.NotNull(audit);
        Assert.Equal("Success", audit.Result);
    }
}
