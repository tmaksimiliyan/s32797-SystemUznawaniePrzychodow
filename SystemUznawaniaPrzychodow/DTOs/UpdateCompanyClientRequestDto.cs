namespace SystemUznawaniaPrzychodow.DTOs;

public record UpdateCompanyClientRequestDto(
    string CompanyName,
    string Address,
    string Email,
    string Phone);
