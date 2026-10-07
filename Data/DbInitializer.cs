using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IWebHostEnvironment? environment = null)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        string[] roles = ["Admin", "Manager", "SalesExecutive"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Permissions
        var permissions = new List<AppPermission>
        {
            new() { Name = "Dashboard.View", Module = "Dashboard", Description = "View dashboard metrics" },
            new() { Name = "Customers.View", Module = "Customers", Description = "View customer records" },
            new() { Name = "Customers.Create", Module = "Customers", Description = "Create customer records" },
            new() { Name = "Customers.Edit", Module = "Customers", Description = "Edit customer records" },
            new() { Name = "Customers.Delete", Module = "Customers", Description = "Deactivate customer records" },
            new() { Name = "Leads.View", Module = "Leads", Description = "View leads" },
            new() { Name = "Leads.Create", Module = "Leads", Description = "Create leads" },
            new() { Name = "Leads.Edit", Module = "Leads", Description = "Edit leads" },
            new() { Name = "Leads.Convert", Module = "Leads", Description = "Convert qualified leads" },
            new() { Name = "Opportunities.View", Module = "Opportunities", Description = "View opportunities" },
            new() { Name = "Opportunities.Create", Module = "Opportunities", Description = "Create opportunities" },
            new() { Name = "Opportunities.Edit", Module = "Opportunities", Description = "Edit opportunities" },
            new() { Name = "FollowUps.Manage", Module = "FollowUps", Description = "Manage follow-ups" },
            new() { Name = "Activities.Manage", Module = "Activities", Description = "Manage activities" },
            new() { Name = "Users.Manage", Module = "Users", Description = "Manage users and roles" },
            new() { Name = "Audit.View", Module = "Audit", Description = "View audit logs" },
            new() { Name = "Reports.View", Module = "Reports", Description = "View CRM reports" },
        };

        foreach (var perm in permissions)
        {
            if (!await context.AppPermissions.AnyAsync(p => p.Name == perm.Name))
            {
                context.AppPermissions.Add(perm);
            }
        }
        await context.SaveChangesAsync();

        // Map Permissions to Roles
        var adminRole = await roleManager.FindByNameAsync("Admin");
        var managerRole = await roleManager.FindByNameAsync("Manager");
        var seRole = await roleManager.FindByNameAsync("SalesExecutive");

        var allPerms = await context.AppPermissions.ToListAsync();
        if (adminRole != null)
        {
            foreach (var p in allPerms)
            {
                if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionId == p.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = p.PermissionId });
                }
            }
        }
        if (managerRole != null)
        {
            var managerPermNames = new[] { "Dashboard.View", "Customers.View", "Customers.Create", "Customers.Edit", "Leads.View", "Leads.Create", "Leads.Edit", "Leads.Convert", "Opportunities.View", "Opportunities.Create", "Opportunities.Edit", "FollowUps.Manage", "Activities.Manage", "Audit.View", "Reports.View" };
            foreach (var p in allPerms.Where(p => managerPermNames.Contains(p.Name)))
            {
                if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == managerRole.Id && rp.PermissionId == p.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = managerRole.Id, PermissionId = p.PermissionId });
                }
            }
        }
        if (seRole != null)
        {
            var sePermNames = new[] { "Dashboard.View", "Customers.View", "Customers.Create", "Customers.Edit", "Leads.View", "Leads.Create", "Leads.Edit", "Leads.Convert", "Opportunities.View", "Opportunities.Create", "Opportunities.Edit", "FollowUps.Manage", "Activities.Manage", "Reports.View" };
            foreach (var p in allPerms.Where(p => sePermNames.Contains(p.Name)))
            {
                if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == seRole.Id && rp.PermissionId == p.PermissionId))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = seRole.Id, PermissionId = p.PermissionId });
                }
            }
        }
        await context.SaveChangesAsync();

        // 3. Seed Users
        string? seedPassword = configuration["SeedData:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            if (environment == null || environment.IsDevelopment())
            {
                seedPassword = "Password@123!";
            }
            else
            {
                // In production, do not create default users without explicitly provided secure configuration
                return;
            }
        }

        // Admin User
        var adminUser = await userManager.FindByEmailAsync("admin@acxiomcrm.com");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin@acxiomcrm.com",
                Email = "admin@acxiomcrm.com",
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(adminUser, seedPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Manager User
        var managerUser = await userManager.FindByEmailAsync("manager@acxiomcrm.com");
        if (managerUser == null)
        {
            managerUser = new ApplicationUser
            {
                UserName = "manager@acxiomcrm.com",
                Email = "manager@acxiomcrm.com",
                FullName = "Sales Manager",
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(managerUser, seedPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, "Manager");
            }
        }

        // Sales Executive 1 (under Manager)
        var sales1User = await userManager.FindByEmailAsync("sales1@acxiomcrm.com");
        if (sales1User == null)
        {
            sales1User = new ApplicationUser
            {
                UserName = "sales1@acxiomcrm.com",
                Email = "sales1@acxiomcrm.com",
                FullName = "Alex Executive",
                EmailConfirmed = true,
                IsActive = true,
                ManagerId = managerUser?.Id,
                CreatedDate = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(sales1User, seedPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(sales1User, "SalesExecutive");
            }
        }

        // Sales Executive 2 (under Manager)
        var sales2User = await userManager.FindByEmailAsync("sales2@acxiomcrm.com");
        if (sales2User == null)
        {
            sales2User = new ApplicationUser
            {
                UserName = "sales2@acxiomcrm.com",
                Email = "sales2@acxiomcrm.com",
                FullName = "Sam Executive",
                EmailConfirmed = true,
                IsActive = true,
                ManagerId = managerUser?.Id,
                CreatedDate = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(sales2User, seedPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(sales2User, "SalesExecutive");
            }
        }

        // 4. Seed Business Entities (Sample customers, leads, opportunities, follow-ups, activities)
        if (!await context.Customers.AnyAsync() && sales1User != null && sales2User != null)
        {
            var cust1 = new Customer
            {
                CustomerCode = "CUST-001",
                CustomerName = "John Doe",
                Email = "john@acme.com",
                Phone = "9876543210",
                CompanyName = "Acme Global Solutions",
                Address = "101 Industrial Park",
                City = "Bengaluru",
                State = "Karnataka",
                Status = "Active",
                Notes = "Key enterprise account with ongoing support needs.",
                OwnerId = sales1User.Id,
                CreatedBy = sales1User.UserName ?? "sales1@acxiomcrm.com",
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var cust2 = new Customer
            {
                CustomerCode = "CUST-002",
                CustomerName = "Jane Smith",
                Email = "jane@beta.com",
                Phone = "9876543211",
                CompanyName = "Beta Technologies",
                Address = "404 Cyber City",
                City = "Hyderabad",
                State = "Telangana",
                Status = "Active",
                Notes = "SaaS technology vendor evaluating CRM expansion.",
                OwnerId = sales2User.Id,
                CreatedBy = sales2User.UserName ?? "sales2@acxiomcrm.com",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            context.Customers.AddRange(cust1, cust2);
            await context.SaveChangesAsync();

            // Seed Leads
            var lead1 = new Lead
            {
                LeadCode = "LEAD-001",
                LeadName = "Charlie Brown",
                Email = "charlie@gamma.com",
                Phone = "9876543212",
                CompanyName = "Gamma Retail Ltd",
                Source = "Website",
                Status = "New",
                Priority = "High",
                ExpectedValue = 50000m,
                Notes = "Requested live product demo via website form.",
                AssignedTo = sales1User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var lead2 = new Lead
            {
                LeadCode = "LEAD-002",
                LeadName = "Dave Clark",
                Email = "dave@delta.com",
                Phone = "9876543213",
                CompanyName = "Delta Logistics",
                Source = "Referral",
                Status = "Qualified",
                Priority = "Medium",
                ExpectedValue = 120000m,
                Notes = "Budget approved, ready for proposal discussion.",
                AssignedTo = sales1User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            var lead3 = new Lead
            {
                LeadCode = "LEAD-003",
                LeadName = "Fiona White",
                Email = "fiona@echo.com",
                Phone = "9876543214",
                CompanyName = "Echo Systems",
                Source = "Cold Call",
                Status = "Contacted",
                Priority = "Low",
                ExpectedValue = 35000m,
                Notes = "Sent brochure, awaiting follow-up call.",
                AssignedTo = sales2User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            context.Leads.AddRange(lead1, lead2, lead3);
            await context.SaveChangesAsync();

            // Seed Opportunities
            var opp1 = new Opportunity
            {
                OpportunityName = "Enterprise Cloud Suite",
                CustomerId = cust1.CustomerId,
                Amount = 75000m,
                Stage = "Qualification",
                Probability = 20,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(45),
                Status = "Open",
                Source = "Website",
                Notes = "Initial requirements gathering underway.",
                AssignedTo = sales1User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var opp2 = new Opportunity
            {
                OpportunityName = "Support & Maintenance SLA",
                CustomerId = cust1.CustomerId,
                Amount = 150000m,
                Stage = "Proposal",
                Probability = 50,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                Status = "Open",
                Source = "Referral",
                Notes = "Formal RFP proposal submitted to procurement.",
                AssignedTo = sales1User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var opp3 = new Opportunity
            {
                OpportunityName = "Security Infrastructure Audit",
                CustomerId = cust2.CustomerId,
                Amount = 95000m,
                Stage = "Negotiation",
                Probability = 80,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
                Status = "Open",
                Source = "Direct Call",
                Notes = "Final commercial terms being reviewed by legal.",
                AssignedTo = sales2User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            var opp4 = new Opportunity
            {
                OpportunityName = "Annual Subscription Renewal",
                CustomerId = cust2.CustomerId,
                Amount = 60000m,
                Stage = "Won",
                Probability = 100,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
                Status = "Won",
                Source = "Existing Client",
                Notes = "Contract signed and payment processed.",
                AssignedTo = sales2User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            context.Opportunities.AddRange(opp1, opp2, opp3, opp4);
            await context.SaveChangesAsync();

            // Seed Follow-ups
            var f1 = new FollowUp
            {
                CustomerId = cust1.CustomerId,
                FollowUpDate = DateTime.UtcNow.AddDays(2),
                FollowUpType = "Call",
                Subject = "Discuss SLA Proposal Terms",
                Remarks = "Review clause 4 SLA options with John.",
                Status = "Planned",
                AssignedTo = sales1User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var f2 = new FollowUp
            {
                LeadId = lead2.LeadId,
                FollowUpDate = DateTime.UtcNow.AddDays(1),
                FollowUpType = "Meeting",
                Subject = "Product Demo Presentation",
                Remarks = "Deliver customized demo for Delta Logistics team.",
                Status = "Planned",
                AssignedTo = sales1User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            var f3 = new FollowUp
            {
                OpportunityId = opp3.OpportunityId,
                FollowUpDate = DateTime.UtcNow.AddDays(-1), // Overdue for testing
                FollowUpType = "Call",
                Subject = "Legal Contract Review Follow-up",
                Remarks = "Follow up on contract redlines.",
                Status = "Planned",
                AssignedTo = sales2User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-3)
            };

            var f4 = new FollowUp
            {
                CustomerId = cust2.CustomerId,
                FollowUpDate = DateTime.UtcNow.AddDays(-5),
                FollowUpType = "Meeting",
                Subject = "Renewal Closing Meeting",
                Remarks = "Executed contract finalized in person.",
                Status = "Completed",
                CompletedDate = DateTime.UtcNow.AddDays(-5),
                AssignedTo = sales2User.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-10)
            };

            context.FollowUps.AddRange(f1, f2, f3, f4);

            // Seed Activities
            var a1 = new Activity
            {
                ActivityType = "Call",
                Subject = "Introductory Call with Acme",
                Description = "Discussed CRM modernization objectives.",
                ActivityDate = DateTime.UtcNow.AddDays(-2),
                CustomerId = cust1.CustomerId,
                AssignedTo = sales1User.Id,
                Status = "Completed"
            };

            var a2 = new Activity
            {
                ActivityType = "Meeting",
                Subject = "Quarterly Business Review",
                Description = "Reviewed SLA performance and adoption metrics.",
                ActivityDate = DateTime.UtcNow.AddDays(-1),
                CustomerId = cust2.CustomerId,
                AssignedTo = sales2User.Id,
                Status = "Completed"
            };

            context.Activities.AddRange(a1, a2);

            // Seed Initial Audit Logs
            context.AuditLogs.Add(new AuditLog
            {
                UserId = adminUser?.Id,
                Action = "Seed",
                Module = "System",
                EntityName = "Database",
                RecordId = "0",
                NewValue = "{\"Message\":\"Initial seed data created successfully.\"}",
                Result = "Success",
                CreatedDate = DateTime.UtcNow,
                IpAddress = "127.0.0.1"
            });

            await context.SaveChangesAsync();
        }
    }
}
