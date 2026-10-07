using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Deactivate, StatusChange, Convert, Login, LoginFailure, Logout, Lockout, Unlock, RoleChange, Reschedule, Complete

    [Required]
    [MaxLength(100)]
    public string Module { get; set; } = string.Empty; // Customer, Lead, Opportunity, FollowUp, Activity, User, Auth, System

    [MaxLength(100)]
    public string? EntityName { get; set; }

    [MaxLength(100)]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [Required]
    [MaxLength(50)]
    public string Result { get; set; } = "Success"; // Success, Failure

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? IpAddress { get; set; }
}
