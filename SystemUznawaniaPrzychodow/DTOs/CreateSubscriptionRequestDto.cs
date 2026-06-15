namespace SystemUznawaniaPrzychodow.DTOs;

public record CreateSubscriptionRequestDto(
    int ClientId,
    int SoftwareId,
    string Name,
    int RenewalPeriodMonths);
