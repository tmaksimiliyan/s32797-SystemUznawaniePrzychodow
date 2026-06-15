namespace SystemUznawaniaPrzychodow.Services;

public interface ITokenService
{
    string GenerateAccessToken(string employeeId, string login, string role);
    string GenerateRefreshToken();
}
