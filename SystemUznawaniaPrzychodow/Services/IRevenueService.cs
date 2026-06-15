using SystemUznawaniaPrzychodow.DTOs;

namespace SystemUznawaniaPrzychodow.Services;

public interface IRevenueService
{
    Task<RevenueResponseDto> GetCurrentRevenueAsync(int? softwareId, string? currency, CancellationToken ct);
    Task<RevenueResponseDto> GetPredictedRevenueAsync(int? softwareId, string? currency, CancellationToken ct);
}
