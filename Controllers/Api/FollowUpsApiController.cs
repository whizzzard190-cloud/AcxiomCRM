using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/followups")]
[Authorize]
public class FollowUpsApiController : ControllerBase
{
    private readonly IFollowUpService _followUpService;

    public FollowUpsApiController(IFollowUpService followUpService)
    {
        _followUpService = followUpService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<FollowUpDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? filter, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _followUpService.GetFollowUpsAsync(User, filter, search, null, null, page, pageSize);
        var dtos = result.Items.Select(ToDto).ToList();
        return Ok(dtos);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFollowUpDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid data.", Status = StatusCodes.Status400BadRequest });
        }

        var f = new FollowUp
        {
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            OpportunityId = dto.OpportunityId,
            FollowUpDate = dto.FollowUpDate,
            FollowUpType = dto.FollowUpType,
            Subject = dto.Subject,
            Remarks = dto.Remarks,
            AssignedTo = dto.AssignedTo ?? "",
            Status = "Planned"
        };

        var (success, error, created) = await _followUpService.CreateFollowUpAsync(User, f);
        if (!success)
        {
            return BadRequest(new ProblemDetails { Title = error ?? "Failed to create follow-up.", Status = StatusCodes.Status400BadRequest });
        }

        var responseDto = ToDto(created!);
        return Created($"/api/followups/{responseDto.FollowUpId}", responseDto);
    }

    private static FollowUpDto ToDto(FollowUp f) => new()
    {
        FollowUpId = f.FollowUpId,
        CustomerId = f.CustomerId,
        CustomerName = f.Customer?.CustomerName,
        LeadId = f.LeadId,
        LeadName = f.Lead?.LeadName,
        OpportunityId = f.OpportunityId,
        OpportunityName = f.Opportunity?.OpportunityName,
        FollowUpDate = f.FollowUpDate,
        FollowUpType = f.FollowUpType,
        Subject = f.Subject,
        Remarks = f.Remarks,
        Status = f.Status,
        IsOverdue = f.IsOverdue,
        AssignedTo = f.AssignedTo,
        AssignedUserName = f.AssignedUser?.FullName,
        CreatedDate = f.CreatedDate
    };
}
