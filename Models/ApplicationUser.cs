using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [MaxLength(450)]
    public string? ManagerId { get; set; }

    [ForeignKey(nameof(ManagerId))]
    public virtual ApplicationUser? Manager { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ApplicationUser> DirectReports { get; set; } = new List<ApplicationUser>();
}
