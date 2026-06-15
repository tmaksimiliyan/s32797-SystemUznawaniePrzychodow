using SystemUznawaniaPrzychodow.DTOs;

namespace SystemUznawaniaPrzychodow.Services;

public interface ISubscriptionService
{
    Task<SubscriptionResponseDto> CreateSubscriptionAsync(CreateSubscriptionRequestDto request, CancellationToken ct);
    Task<SubscriptionPaymentResponseDto> RenewAsync(RenewSubscriptionRequestDto request, CancellationToken ct);
}
