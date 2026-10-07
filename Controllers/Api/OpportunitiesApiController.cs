using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : ControllerBase
{
    private readonly IOpportunityService _opportunityService;

    public OpportunitiesApiController(IOpportunityService opportunityService)
    {
        _opportunityService = opportunityService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OpportunityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? stage, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _opportunityService.GetOpportunitiesAsync(User, search, stage, null, null, null, page, pageSize);
        var dtos = result.Items.Select(ToDto).ToList();
        return Ok(dtos);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOpportunityDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid data.", Status = StatusCodes.Status400BadRequest });
        }

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Source = dto.Source,
            Notes = dto.Notes,
            AssignedTo = dto.AssignedTo ?? ""
        };

        var (success, error, created) = await _opportunityService.CreateOpportunityAsync(User, opp);
        if (!success)
        {
            return BadRequest(new ProblemDetails { Title = error ?? "Failed to create opportunity.", Status = StatusCodes.Status400BadRequest });
        }

        var responseDto = ToDto(created!);
        return Created($"/api/opportunities/{responseDto.OpportunityId}", responseDto);
    }

    private static OpportunityDto ToDto(Opportunity o) => new()
    {
        OpportunityId = o.OpportunityId,
        OpportunityName = o.OpportunityName,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.CustomerName,
        LeadId = o.LeadId,
        Amount = o.Amount,
        Stage = o.Stage,
        Probability = o.Probability,
        WeightedAmount = o.WeightedAmount,
        ExpectedCloseDate = o.ExpectedCloseDate,
        Status = o.Status,
        Source = o.Source,
        Notes = o.Notes,
        AssignedTo = o.AssignedTo,
        AssignedUserName = o.AssignedUser?.FullName,
        CreatedDate = o.CreatedDate
    };
}
