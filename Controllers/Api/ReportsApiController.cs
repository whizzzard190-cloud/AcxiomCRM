using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsApiController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsApiController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("pipeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPipelineReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var (stageWise, ownerWise, _) = await _reportService.GetPipelineReportAsync(User, fromDate, toDate);
        return Ok(new
        {
            StageWise = stageWise,
            OwnerWise = ownerWise
        });
    }
}
