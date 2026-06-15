namespace SystemUznawaniaPrzychodow.DTOs;

public record SubscriptionPaymentResponseDto(
    int Id,
    int SubscriptionId,
    decimal Amount,
    DateTime Date,
    DateTime PeriodStart,
    DateTime PeriodEnd);
