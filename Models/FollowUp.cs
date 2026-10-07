using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models;

public class FollowUp : IValidatableObject
{
    [Key]
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }

    public int? LeadId { get; set; }

    [ForeignKey(nameof(LeadId))]
    public virtual Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }

    [ForeignKey(nameof(OpportunityId))]
    public virtual Opportunity? Opportunity { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Task

    [Required]
    [MaxLength(150)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Remarks { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

    [Required]
    [MaxLength(450)]
    public string AssignedTo { get; set; } = string.Empty;

    [ForeignKey(nameof(AssignedTo))]
    public virtual ApplicationUser? AssignedUser { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }

    [NotMapped]
    public bool IsOverdue => Status == "Planned" && FollowUpDate.Date < DateTime.UtcNow.Date;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId == null && LeadId == null && OpportunityId == null)
        {
            yield return new ValidationResult("At least one association (Customer, Lead, or Opportunity) is required.",
                new[] { nameof(CustomerId), nameof(LeadId), nameof(OpportunityId) });
        }
    }
}
