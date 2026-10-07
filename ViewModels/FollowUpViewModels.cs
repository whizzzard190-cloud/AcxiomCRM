using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class RescheduleFollowUpViewModel
{
    public int FollowUpId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime OldFollowUpDate { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    public DateTime NewFollowUpDate { get; set; } = DateTime.UtcNow.AddDays(1);

    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public class CompleteFollowUpViewModel
{
    public int FollowUpId { get; set; }
    public string Subject { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? CompletionRemarks { get; set; }
}
