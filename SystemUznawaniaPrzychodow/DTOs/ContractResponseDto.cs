namespace SystemUznawaniaPrzychodow.DTOs;

public record ContractResponseDto(
    int Id,
    int ClientId,
    int SoftwareId,
    string SoftwareName,
    string SoftwareVersion,
    DateTime StartDate,
    DateTime EndDate,
    decimal Price,
    int TotalSupportYears,
    bool IsSigned,
    decimal TotalPaid);
