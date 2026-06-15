using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController(IClientService clientService) : ControllerBase
{
    [HttpPost("individual")]
    public async Task<IActionResult> AddIndividualClient([FromBody] CreateIndividualClientRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await clientService.AddIndividualClientAsync(request, ct);
            return Created($"api/clients/{result.Id}", result);
        }
        catch (ConflictException e) { return Conflict(e.Message); }
    }

    [HttpPost("company")]
    public async Task<IActionResult> AddCompanyClient([FromBody] CreateCompanyClientRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await clientService.AddCompanyClientAsync(request, ct);
            return Created($"api/clients/{result.Id}", result);
        }
        catch (ConflictException e) { return Conflict(e.Message); }
    }

    [HttpPut("individual/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateIndividualClient([FromRoute] int id, [FromBody] UpdateIndividualClientRequestDto request, CancellationToken ct)
    {
        try
        {
            await clientService.UpdateIndividualClientAsync(id, request, ct);
            return NoContent();
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
    }

    [HttpPut("company/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCompanyClient([FromRoute] int id, [FromBody] UpdateCompanyClientRequestDto request, CancellationToken ct)
    {
        try
        {
            await clientService.UpdateCompanyClientAsync(id, request, ct);
            return NoContent();
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteClient([FromRoute] int id, CancellationToken ct)
    {
        try
        {
            await clientService.DeleteClientAsync(id, ct);
            return NoContent();
        }
        catch (NotFoundException e) { return NotFound(e.Message); }
        catch (ConflictException e) { return Conflict(e.Message); }
    }
}
