using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController(ISubscriptionService subscriptionService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await subscriptionService.CreateSubscriptionAsync(request, ct);
            return Created($"api/subscriptions/{result.Id}", result);
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
        catch (ConflictException e) { return Conflict(e.Message); }
        catch (BadRequestException e) { return BadRequest(e.Message); }
    }

    [HttpPost("renew")]
    public async Task<IActionResult> Renew([FromBody] RenewSubscriptionRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await subscriptionService.RenewAsync(request, ct);
            return Ok(result);
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
        catch (ConflictException e) { return Conflict(e.Message); }
        catch (BadRequestException e) { return BadRequest(e.Message); }
    }
}
