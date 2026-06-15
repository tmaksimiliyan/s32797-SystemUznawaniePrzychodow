namespace SystemUznawaniaPrzychodow.DTOs;

public record CreateCompanyClientRequestDto(
    string CompanyName,
    string Address,
    string Email,
    string Phone,
    string Krs);
