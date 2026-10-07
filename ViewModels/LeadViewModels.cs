using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class ConvertLeadViewModel
{
    public int LeadId { get; set; }
    public string LeadCode { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CompanyName { get; set; }

    // Customer fields
    [Required(ErrorMessage = "Customer Name is required.")]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    // Optional Opportunity fields
    public bool CreateOpportunity { get; set; } = true;

    [MaxLength(150)]
    public string? OpportunityName { get; set; }

    [Range(0, 100000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 20;

    [DataType(DataType.Date)]
    public DateTime? ExpectedCloseDate { get; set; }
}

public class ChangeLeadStatusViewModel
{
    public int LeadId { get; set; }

    [Required]
    public string NewStatus { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}
