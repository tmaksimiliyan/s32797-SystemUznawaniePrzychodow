namespace SystemUznawaniaPrzychodow.DTOs;

public record ClientResponseDto(
    int Id,
    string Type,
    string Address,
    string Email,
    string Phone,
    string? FirstName,
    string? LastName,
    string? Pesel,
    string? CompanyName,
    string? Krs);
