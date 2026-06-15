using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
public class ContractsController(IContractService contractService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateContract([FromBody] CreateContractRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await contractService.CreateContractAsync(request, ct);
            return Created($"api/contracts/{result.Id}", result);
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
        catch (ConflictException e) { return Conflict(e.Message); }
        catch (BadRequestException e) { return BadRequest(e.Message); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteContract([FromRoute] int id, CancellationToken ct)
    {
        try
        {
            await contractService.DeleteContractAsync(id, ct);
            return NoContent();
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
        catch (ConflictException e) { return Conflict(e.Message); }
    }
}
