namespace SystemUznawaniaPrzychodow.DTOs;

public record SubscriptionResponseDto(
    int Id,
    string Name,
    int ClientId,
    int SoftwareId,
    string SoftwareName,
    int RenewalPeriodMonths,
    decimal PricePerRenewal,
    DateTime StartDate,
    bool IsActive);
