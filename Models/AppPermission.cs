using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

public class AppPermission
{
    [Key]
    public int PermissionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Module { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    [Key]
    public int RolePermissionId { get; set; }

    [Required]
    [MaxLength(450)]
    public string RoleId { get; set; } = string.Empty;

    [ForeignKey(nameof(RoleId))]
    public virtual IdentityRole? Role { get; set; }

    public int PermissionId { get; set; }

    [ForeignKey(nameof(PermissionId))]
    public virtual AppPermission? Permission { get; set; }
}
