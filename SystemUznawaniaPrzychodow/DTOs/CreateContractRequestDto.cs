namespace SystemUznawaniaPrzychodow.DTOs;

public record CreateContractRequestDto(
    int ClientId,
    int SoftwareId,
    DateTime StartDate,
    DateTime EndDate,
    int AdditionalSupportYears);
