namespace SystemUznawaniaPrzychodow.Models;

public enum EmployeeRole
{
    Admin,
    Standard
}

public class Employee
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public EmployeeRole Role { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
}
