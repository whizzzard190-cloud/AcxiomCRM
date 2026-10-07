using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.Extensions.Configuration;

namespace AcxiomCRM.Tests;

public class ValidationTests
{
    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, results, true);
        return results;
    }

    [Fact]
    public void G001_CustomerNameBlank_RejectsValidation()
    {
        var customer = new Customer
        {
            CustomerCode = "CUST-TEST",
            CustomerName = "", // Blank name
            Email = "valid@example.com",
            Phone = "9876543210",
            OwnerId = "user-1",
            CreatedBy = "user-1"
        };

        var results = ValidateModel(customer);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Customer.CustomerName)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missingatsign.com")]
    [InlineData("@nodomain.com")]
    public void G002_InvalidCustomerEmail_RejectsValidation(string invalidEmail)
    {
        var customer = new Customer
        {
            CustomerCode = "CUST-TEST",
            CustomerName = "Valid Name",
            Email = invalidEmail,
            Phone = "9876543210",
            OwnerId = "user-1",
            CreatedBy = "user-1"
        };

        var results = ValidateModel(customer);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Customer.Email)));
    }

    [Theory]
    [InlineData("12345")] // Too short
    [InlineData("5876543210")] // Starts with 5 (must start with 6-9)
    [InlineData("98765432101")] // 11 digits
    [InlineData("abcdefghij")] // Non-digits
    public void G003_InvalidCustomerPhone_RejectsValidation(string invalidPhone)
    {
        var customer = new Customer
        {
            CustomerCode = "CUST-TEST",
            CustomerName = "Valid Name",
            Email = "valid@example.com",
            Phone = invalidPhone,
            OwnerId = "user-1",
            CreatedBy = "user-1"
        };

        var results = ValidateModel(customer);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Customer.Phone)));
    }

    [Fact]
    public async Task G006_OpportunityAmountZero_RejectsValidation()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, _, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerCode = "CUST-001",
            CustomerName = "Acme Corp",
            Email = "acme@example.com",
            Phone = "9876543210",
            OwnerId = admin.Id,
            CreatedBy = admin.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var oppService = new OpportunityService(context, authService, auditService);

        var opp = new Opportunity
        {
            OpportunityName = "Zero Amount Deal",
            CustomerId = customer.CustomerId,
            Amount = 0m, // Invalid: must be > 0
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            AssignedTo = admin.Id
        };

        var (success, error, _) = await oppService.CreateOpportunityAsync(principal, opp);
        Assert.False(success);
        Assert.Equal("Opportunity Amount must be greater than 0.", error);
    }

    [Fact]
    public async Task G007_OpportunityAmountNegative_RejectsValidation()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, _, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerCode = "CUST-002",
            CustomerName = "Beta Corp",
            Email = "beta@example.com",
            Phone = "9876543211",
            OwnerId = admin.Id,
            CreatedBy = admin.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var oppService = new OpportunityService(context, authService, auditService);

        var opp = new Opportunity
        {
            OpportunityName = "Negative Deal",
            CustomerId = customer.CustomerId,
            Amount = -500m,
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            AssignedTo = admin.Id
        };

        var (success, error, _) = await oppService.CreateOpportunityAsync(principal, opp);
        Assert.False(success);
        Assert.Equal("Opportunity Amount must be greater than 0.", error);
    }

    [Fact]
    public async Task G008_OpportunityProbabilityAbove100_RejectsValidation()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, _, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerCode = "CUST-003",
            CustomerName = "Gamma Corp",
            Email = "gamma@example.com",
            Phone = "9876543212",
            OwnerId = admin.Id,
            CreatedBy = admin.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var oppService = new OpportunityService(context, authService, auditService);

        var opp = new Opportunity
        {
            OpportunityName = "High Probability Deal",
            CustomerId = customer.CustomerId,
            Amount = 1000m,
            Stage = "Qualification",
            Probability = 101, // Invalid
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            AssignedTo = admin.Id
        };

        var (success, error, _) = await oppService.CreateOpportunityAsync(principal, opp);
        Assert.False(success);
        Assert.Equal("Probability must be between 0 and 100.", error);
    }

    [Fact]
    public async Task G009_OpportunityProbabilityNegative_RejectsValidation()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, _, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerCode = "CUST-004",
            CustomerName = "Delta Corp",
            Email = "delta@example.com",
            Phone = "9876543213",
            OwnerId = admin.Id,
            CreatedBy = admin.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var oppService = new OpportunityService(context, authService, auditService);

        var opp = new Opportunity
        {
            OpportunityName = "Negative Probability Deal",
            CustomerId = customer.CustomerId,
            Amount = 1000m,
            Stage = "Qualification",
            Probability = -1, // Invalid
            ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
            AssignedTo = admin.Id
        };

        var (success, error, _) = await oppService.CreateOpportunityAsync(principal, opp);
        Assert.False(success);
        Assert.Equal("Probability must be between 0 and 100.", error);
    }

    [Fact]
    public async Task G010_ActiveOpportunityCloseDateYesterday_RejectsValidation()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, _, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerCode = "CUST-005",
            CustomerName = "Epsilon Corp",
            Email = "epsilon@example.com",
            Phone = "9876543214",
            OwnerId = admin.Id,
            CreatedBy = admin.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var oppService = new OpportunityService(context, authService, auditService);

        var opp = new Opportunity
        {
            OpportunityName = "Past Date Deal",
            CustomerId = customer.CustomerId,
            Amount = 5000m,
            Stage = "Qualification", // Active stage
            Probability = 50,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-1), // Yesterday
            AssignedTo = admin.Id
        };

        var (success, error, _) = await oppService.CreateOpportunityAsync(principal, opp);
        Assert.False(success);
        Assert.Equal("Expected Close Date cannot be in the past.", error);
    }

    [Fact]
    public async Task G011_FollowUpYesterday_RejectsValidation()
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var (admin, _, _, _) = await TestHelper.SeedUsersAsync(context);
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerCode = "CUST-006",
            CustomerName = "Zeta Corp",
            Email = "zeta@example.com",
            Phone = "9876543215",
            OwnerId = admin.Id,
            CreatedBy = admin.Id
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor(principal));
        var followUpService = new FollowUpService(context, authService, auditService);

        var followUp = new FollowUp
        {
            CustomerId = customer.CustomerId,
            FollowUpDate = DateTime.UtcNow.AddDays(-1), // Yesterday
            FollowUpType = "Call",
            Subject = "Check in call",
            Status = "Planned",
            AssignedTo = admin.Id
        };

        var (success, error, _) = await followUpService.CreateFollowUpAsync(principal, followUp);
        Assert.False(success);
        Assert.Equal("Follow-up date cannot be earlier than today.", error);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("UnknownStatus")]
    [InlineData("Active")]
    public void G012_InvalidLeadStatus_RejectsValidation(string invalidStatus)
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());
        var leadService = new LeadService(context, authService, auditService, new ConfigurationManager());

        Assert.False(leadService.IsValidStatus(invalidStatus));
    }

    [Theory]
    [InlineData("New", "Converted")] // Invalid direct skip
    [InlineData("New", "Qualified")] // Must go to Contacted first
    [InlineData("Converted", "Contacted")] // Converted is terminal
    [InlineData("Lost", "Qualified")] // Lost is terminal
    public void G013_InvalidLeadTransition_RejectsValidation(string currentStatus, string nextStatus)
    {
        using var context = TestHelper.CreateInMemoryDbContext();
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());
        var leadService = new LeadService(context, authService, auditService, new ConfigurationManager());

        Assert.False(leadService.IsValidTransition(currentStatus, nextStatus));
    }

    [Fact]
    public void G040_LeadInvalidFields_RejectsValidation()
    {
        // 1. Invalid Email
        var leadBadEmail = new Lead
        {
            LeadCode = "LEAD-1",
            LeadName = "Lead One",
            Email = "bad-email",
            Phone = "9876543210",
            ExpectedValue = 50000,
            AssignedTo = "user-1"
        };
        var resultsEmail = ValidateModel(leadBadEmail);
        Assert.Contains(resultsEmail, r => r.MemberNames.Contains(nameof(Lead.Email)));

        // 2. Invalid Phone
        var leadBadPhone = new Lead
        {
            LeadCode = "LEAD-2",
            LeadName = "Lead Two",
            Email = "good@lead.com",
            Phone = "12345", // Must start with 6-9, 10 digits
            ExpectedValue = 50000,
            AssignedTo = "user-1"
        };
        var resultsPhone = ValidateModel(leadBadPhone);
        Assert.Contains(resultsPhone, r => r.MemberNames.Contains(nameof(Lead.Phone)));

        // 3. ExpectedValue out of range (> 100,000,000)
        var leadBadValue = new Lead
        {
            LeadCode = "LEAD-3",
            LeadName = "Lead Three",
            Email = "good@lead.com",
            Phone = "9876543210",
            ExpectedValue = 150000000m,
            AssignedTo = "user-1"
        };
        var resultsValue = ValidateModel(leadBadValue);
        Assert.Contains(resultsValue, r => r.MemberNames.Contains(nameof(Lead.ExpectedValue)));
    }
}
