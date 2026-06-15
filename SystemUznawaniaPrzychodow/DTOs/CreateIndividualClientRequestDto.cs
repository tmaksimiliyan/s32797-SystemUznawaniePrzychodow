namespace SystemUznawaniaPrzychodow.DTOs;

public record CreateIndividualClientRequestDto(
    string FirstName,
    string LastName,
    string Address,
    string Email,
    string Phone,
    string Pesel);
