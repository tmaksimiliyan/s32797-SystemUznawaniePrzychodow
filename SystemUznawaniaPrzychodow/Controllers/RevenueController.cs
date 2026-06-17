using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Controllers;

[ApiController]
[Route("api/revenue")]
[Authorize]
public class RevenueController(IRevenueService revenueService) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent([FromQuery] string? currency, [FromQuery] int? softwareId, CancellationToken ct)
    {
        try
        {
            var result = await revenueService.GetCurrentRevenueAsync(softwareId, currency, ct);
            return Ok(result);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet("predicted")]
    public async Task<IActionResult> GetPredicted([FromQuery] string? currency, [FromQuery] int? softwareId, CancellationToken ct)
    {
        try
        {
            var result = await revenueService.GetPredictedRevenueAsync(softwareId, currency, ct);
            return Ok(result);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
    }
}
