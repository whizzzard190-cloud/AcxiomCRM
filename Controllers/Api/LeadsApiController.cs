using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly ILeadService _leadService;

    public LeadsApiController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<LeadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _leadService.GetLeadsAsync(User, search, status, null, null, null, page, pageSize);
        var dtos = result.Items.Select(ToDto).ToList();
        return Ok(dtos);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLeadDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid lead data.", Status = StatusCodes.Status400BadRequest });
        }

        var lead = new Lead
        {
            LeadName = dto.LeadName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Source = dto.Source,
            Status = dto.Status,
            Priority = dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            Notes = dto.Notes,
            AssignedTo = dto.AssignedTo ?? ""
        };

        var (success, error, created) = await _leadService.CreateLeadAsync(User, lead);
        if (!success)
        {
            return BadRequest(new ProblemDetails { Title = error ?? "Failed to create lead.", Status = StatusCodes.Status400BadRequest });
        }

        var responseDto = ToDto(created!);
        return Created($"/api/leads/{responseDto.LeadId}", responseDto);
    }

    private static LeadDto ToDto(Lead l) => new()
    {
        LeadId = l.LeadId,
        LeadCode = l.LeadCode,
        LeadName = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Source = l.Source,
        Status = l.Status,
        Priority = l.Priority,
        ExpectedValue = l.ExpectedValue,
        Notes = l.Notes,
        AssignedTo = l.AssignedTo,
        AssignedUserName = l.AssignedUser?.FullName,
        CreatedDate = l.CreatedDate
    };
}
