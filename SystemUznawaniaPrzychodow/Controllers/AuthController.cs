using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    ITokenService tokenService,
    IPasswordService passwordService) : ControllerBase
{
    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequestDto request, CancellationToken ct)
    {
        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.Login == request.Login.Trim().ToLowerInvariant(), ct);

        if (employee is null || !passwordService.VerifyHashedPassword(employee.PasswordHash, request.Password))
        {
            return Unauthorized("Nieprawidłowy login lub hasło.");
        }

        var accessToken = tokenService.GenerateAccessToken(employee.Id.ToString(), employee.Login, employee.Role.ToString());
        var refreshToken = tokenService.GenerateRefreshToken();

        employee.RefreshToken = refreshToken;
        employee.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await db.SaveChangesAsync(ct);

        AppendRefreshTokenCookie(refreshToken);
        return Ok(new { accessToken });
    }

    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequestDto request, CancellationToken ct)
    {
        var normalizedLogin = request.Login.Trim().ToLowerInvariant();

        if (await db.Employees.AnyAsync(e => e.Login == normalizedLogin, ct))
        {
            return Conflict("Login jest już zajęty.");
        }

        if (!Enum.TryParse<EmployeeRole>(request.Role, ignoreCase: true, out var role))
        {
            return BadRequest("Nieprawidłowa rola. Dostępne: Admin, Standard.");
        }

        var employee = new Employee
        {
            Login = normalizedLogin,
            PasswordHash = passwordService.HashPassword(request.Password),
            Role = role
        };

        var accessToken = tokenService.GenerateAccessToken(employee.Id.ToString(), employee.Login, employee.Role.ToString());
        var refreshToken = tokenService.GenerateRefreshToken();

        employee.RefreshToken = refreshToken;
        employee.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        await db.Employees.AddAsync(employee, ct);
        await db.SaveChangesAsync(ct);

        AppendRefreshTokenCookie(refreshToken);
        return Ok(new { accessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized("Brak refresh tokenu.");
        }

        var employee = await db.Employees
            .FirstOrDefaultAsync(e => e.RefreshToken == refreshToken && e.RefreshTokenExpiry >= DateTime.UtcNow, ct);

        if (employee is null)
        {
            return Unauthorized("Nieprawidłowy lub wygasły refresh token.");
        }

        var accessToken = tokenService.GenerateAccessToken(employee.Id.ToString(), employee.Login, employee.Role.ToString());
        var newRefreshToken = tokenService.GenerateRefreshToken();

        employee.RefreshToken = newRefreshToken;
        employee.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await db.SaveChangesAsync(ct);

        AppendRefreshTokenCookie(newRefreshToken);
        return Ok(new { accessToken });
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOut(CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refreshToken"];

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.RefreshToken == refreshToken, ct);

            if (employee is not null)
            {
                employee.RefreshToken = null;
                employee.RefreshTokenExpiry = null;
                await db.SaveChangesAsync(ct);
            }
        }

        Response.Cookies.Delete("refreshToken", new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
        return Ok();
    }

    private void AppendRefreshTokenCookie(string refreshToken) =>
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            Expires = DateTime.UtcNow.AddDays(7),
            SameSite = SameSiteMode.Strict
        });
}
