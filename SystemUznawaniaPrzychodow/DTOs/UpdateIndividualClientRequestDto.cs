namespace SystemUznawaniaPrzychodow.DTOs;

public record UpdateIndividualClientRequestDto(
    string FirstName,
    string LastName,
    string Address,
    string Email,
    string Phone);
