using AcxiomCRM.Authorization;
using AcxiomCRM.Controllers.Api;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Tests;

public class CustomerAndApiTests
{
    private static async Task<(CustomerService service, ApplicationDbContext context, ApplicationUser admin, ApplicationUser se1, ApplicationUser se2)> CreateSetupAsync()
    {
        var context = TestHelper.CreateInMemoryDbContext();
        var (admin, manager, se1, se2) = await TestHelper.SeedUsersAsync(context);
        var authService = TestHelper.CreateResourceAuthorizationService(context);
        var auditService = new AuditService(context, TestHelper.CreateMockHttpContextAccessor());
        var customerService = new CustomerService(context, authService, auditService);
        return (customerService, context, admin, se1, se2);
    }

    [Fact]
    public async Task G004_DuplicateCustomerEmail_RejectsCreation()
    {
        var (service, _, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer1 = new Customer
        {
            CustomerName = "First Customer",
            Email = "duplicate@acxiom.local",
            Phone = "9876543210",
            OwnerId = admin.Id
        };
        var (s1, _, _) = await service.CreateCustomerAsync(principal, customer1);
        Assert.True(s1);

        var customer2 = new Customer
        {
            CustomerName = "Second Customer",
            Email = "DUPLICATE@acxiom.local", // Case-insensitive duplicate email
            Phone = "9876543211",
            OwnerId = admin.Id
        };
        var (s2, error2, _) = await service.CreateCustomerAsync(principal, customer2);
        Assert.False(s2);
        Assert.Contains("email address already exists", error2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task G005_DuplicateCustomerPhone_RejectsCreation()
    {
        var (service, _, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer1 = new Customer
        {
            CustomerName = "Customer One",
            Email = "phone1@acxiom.local",
            Phone = "9876543210",
            OwnerId = admin.Id
        };
        var (s1, _, _) = await service.CreateCustomerAsync(principal, customer1);
        Assert.True(s1);

        var customer2 = new Customer
        {
            CustomerName = "Customer Two",
            Email = "phone2@acxiom.local",
            Phone = "9876543210", // Duplicate phone number
            OwnerId = admin.Id
        };
        var (s2, error2, _) = await service.CreateCustomerAsync(principal, customer2);
        Assert.False(s2);
        Assert.Contains("phone number already exists", error2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task G021_CreateCustomer_GeneratesAuditEntry()
    {
        var (service, context, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerName = "Audited Customer",
            Email = "auditcreate@acxiom.local",
            Phone = "9876543220",
            OwnerId = admin.Id
        };

        var (success, _, created) = await service.CreateCustomerAsync(principal, customer);
        Assert.True(success);

        var auditLog = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "Customer" && a.Action == "Create" && a.RecordId == created!.CustomerId.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal("Success", auditLog.Result);
        Assert.Equal(admin.Id, auditLog.UserId);
    }

    [Fact]
    public async Task G022_UpdateCustomer_GeneratesAuditEntry()
    {
        var (service, context, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerName = "Pre-Update Customer",
            Email = "preupdate@acxiom.local",
            Phone = "9876543221",
            OwnerId = admin.Id
        };
        var (_, _, created) = await service.CreateCustomerAsync(principal, customer);

        created!.CustomerName = "Post-Update Customer";
        var (success, _, _) = await service.UpdateCustomerAsync(principal, created);
        Assert.True(success);

        var updateAudit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "Customer" && a.Action == "Update" && a.RecordId == created.CustomerId.ToString());
        Assert.NotNull(updateAudit);
        Assert.Equal("Success", updateAudit.Result);
        Assert.Contains("Post-Update Customer", updateAudit.NewValue);
    }

    [Fact]
    public async Task G023_DeactivateCustomer_GeneratesAuditEntry()
    {
        var (service, context, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerName = "To Deactivate",
            Email = "deact@acxiom.local",
            Phone = "9876543222",
            OwnerId = admin.Id
        };
        var (_, _, created) = await service.CreateCustomerAsync(principal, customer);

        var (success, _) = await service.DeactivateCustomerAsync(principal, created!.CustomerId);
        Assert.True(success);

        var deactAudit = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.Module == "Customer" && a.Action == "Deactivate" && a.RecordId == created.CustomerId.ToString());
        Assert.NotNull(deactAudit);
        Assert.Equal("Success", deactAudit.Result);
    }

    [Fact]
    public async Task G035_SqlInjectionStringInSearch_NoExtraRowsOrCrash()
    {
        var (service, _, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerName = "Standard Customer",
            Email = "standard@acxiom.local",
            Phone = "9876543230",
            OwnerId = admin.Id
        };
        await service.CreateCustomerAsync(principal, customer);

        // Typical SQL Injection attack patterns
        string[] sqlPayloads =
        {
            "' OR '1'='1' --",
            "'; DROP TABLE Customers; --",
            "\" OR \"1\"=\"1",
            "1' UNION SELECT * FROM Users --"
        };

        foreach (var payload in sqlPayloads)
        {
            var result = await service.GetCustomersAsync(principal, search: payload);
            Assert.NotNull(result);
            // Search should safely treat the payload as literal search string and return 0 matches
            Assert.Empty(result.Items);
        }
    }

    [Fact]
    public async Task G028_AuthorizedGetApiCustomers_ReturnsDtoJson()
    {
        var (service, _, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerName = "Api Customer",
            Email = "apicust@acxiom.local",
            Phone = "9876543235",
            OwnerId = admin.Id
        };
        await service.CreateCustomerAsync(principal, customer);

        var controller = new CustomersApiController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
            }
        };

        var actionResult = await controller.GetAll(null);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dtos = Assert.IsType<List<CustomerDto>>(okResult.Value);

        Assert.NotEmpty(dtos);
        Assert.Contains(dtos, d => d.CustomerName == "Api Customer" && d.Email == "apicust@acxiom.local");
    }

    [Fact]
    public async Task G038_ApiGetUnknownId_Returns404NotFound()
    {
        var (service, _, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var controller = new CustomersApiController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
            }
        };

        var actionResult = await controller.GetById(999999);
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task G039_ApiDuplicateCustomer_Returns409Conflict()
    {
        var (service, _, admin, _, _) = await CreateSetupAsync();
        var principal = TestHelper.CreateUserPrincipal(admin.Id, admin.Email!, "Admin");

        var customer = new Customer
        {
            CustomerName = "Existing Customer",
            Email = "conflict@acxiom.local",
            Phone = "9876543240",
            OwnerId = admin.Id
        };
        await service.CreateCustomerAsync(principal, customer);

        var controller = new CustomersApiController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
            }
        };

        var duplicateDto = new CreateCustomerDto
        {
            CustomerName = "New Duplicate",
            Email = "conflict@acxiom.local", // Duplicate
            Phone = "9876543241"
        };

        var actionResult = await controller.Create(duplicateDto);
        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(conflictResult.Value);
        Assert.Equal(409, problem.Status);
    }
}
