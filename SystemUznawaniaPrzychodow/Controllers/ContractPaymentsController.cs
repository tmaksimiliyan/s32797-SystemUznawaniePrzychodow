using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Controllers;

[ApiController]
[Route("api/payments/contracts")]
[Authorize]
public class ContractPaymentsController(IContractPaymentService paymentService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Pay([FromBody] CreateContractPaymentRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await paymentService.PayAsync(request, ct);
            return Ok(result);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (ConflictException e)
        {
            return Conflict(e.Message);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
    }
}
