using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SystemUznawaniaPrzychodow.Services;

public class TokenService(IConfiguration configuration) : ITokenService
{
    public string GenerateAccessToken(string employeeId, string login, string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, employeeId),
            new Claim(ClaimTypes.Name, login),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = credentials,
            Expires = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("JWT:ExpirationMinutes")),
            Issuer = configuration["JWT:Issuer"],
            Audience = configuration["JWT:Audience"]
        });

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[96];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
