using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppPermission> AppPermissions => Set<AppPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser hierarchy
        builder.Entity<ApplicationUser>(b =>
        {
            b.HasOne(u => u.Manager)
             .WithMany(u => u.DirectReports)
             .HasForeignKey(u => u.ManagerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Customer constraints and indexes
        builder.Entity<Customer>(b =>
        {
            b.HasIndex(c => c.CustomerCode).IsUnique();
            b.HasIndex(c => c.Email).IsUnique();
            b.HasIndex(c => c.Phone).IsUnique();
            b.HasIndex(c => c.OwnerId);
            b.HasIndex(c => c.Status);

            b.HasOne(c => c.Owner)
             .WithMany()
             .HasForeignKey(c => c.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Lead constraints and indexes
        builder.Entity<Lead>(b =>
        {
            b.HasIndex(l => l.LeadCode).IsUnique();
            b.HasIndex(l => l.Email);
            b.HasIndex(l => l.Phone);
            b.HasIndex(l => l.Status);
            b.HasIndex(l => l.AssignedTo);

            b.HasOne(l => l.AssignedUser)
             .WithMany()
             .HasForeignKey(l => l.AssignedTo)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(l => l.ConvertedCustomer)
             .WithMany()
             .HasForeignKey(l => l.ConvertedCustomerId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(l => l.ConvertedOpportunity)
             .WithMany()
             .HasForeignKey(l => l.ConvertedOpportunityId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Opportunity constraints and indexes
        builder.Entity<Opportunity>(b =>
        {
            b.HasIndex(o => o.CustomerId);
            b.HasIndex(o => o.LeadId);
            b.HasIndex(o => o.AssignedTo);
            b.HasIndex(o => o.Stage);
            b.HasIndex(o => o.Status);

            b.HasOne(o => o.Customer)
             .WithMany(c => c.Opportunities)
             .HasForeignKey(o => o.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(o => o.Lead)
             .WithMany()
             .HasForeignKey(o => o.LeadId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(o => o.AssignedUser)
             .WithMany()
             .HasForeignKey(o => o.AssignedTo)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // FollowUp constraints and indexes
        builder.Entity<FollowUp>(b =>
        {
            b.HasIndex(f => f.CustomerId);
            b.HasIndex(f => f.LeadId);
            b.HasIndex(f => f.OpportunityId);
            b.HasIndex(f => f.AssignedTo);
            b.HasIndex(f => f.FollowUpDate);
            b.HasIndex(f => f.Status);

            b.HasOne(f => f.Customer)
             .WithMany(c => c.FollowUps)
             .HasForeignKey(f => f.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(f => f.Lead)
             .WithMany(l => l.FollowUps)
             .HasForeignKey(f => f.LeadId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(f => f.Opportunity)
             .WithMany(o => o.FollowUps)
             .HasForeignKey(f => f.OpportunityId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(f => f.AssignedUser)
             .WithMany()
             .HasForeignKey(f => f.AssignedTo)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Activity constraints and indexes
        builder.Entity<Activity>(b =>
        {
            b.HasIndex(a => a.CustomerId);
            b.HasIndex(a => a.LeadId);
            b.HasIndex(a => a.AssignedTo);
            b.HasIndex(a => a.ActivityDate);

            b.HasOne(a => a.Customer)
             .WithMany(c => c.Activities)
             .HasForeignKey(a => a.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(a => a.Lead)
             .WithMany(l => l.Activities)
             .HasForeignKey(a => a.LeadId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(a => a.AssignedUser)
             .WithMany()
             .HasForeignKey(a => a.AssignedTo)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog indexes
        builder.Entity<AuditLog>(b =>
        {
            b.HasIndex(al => al.UserId);
            b.HasIndex(al => al.Module);
            b.HasIndex(al => al.Action);
            b.HasIndex(al => al.CreatedDate);

            b.HasOne(al => al.User)
             .WithMany()
             .HasForeignKey(al => al.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // RolePermission
        builder.Entity<RolePermission>(b =>
        {
            b.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        });
    }
}
