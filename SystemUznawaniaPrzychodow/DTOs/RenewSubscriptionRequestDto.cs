namespace SystemUznawaniaPrzychodow.DTOs;

public record RenewSubscriptionRequestDto(
    int SubscriptionId,
    int ClientId,
    decimal Amount);
