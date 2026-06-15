using SystemUznawaniaPrzychodow.DTOs;

namespace SystemUznawaniaPrzychodow.Services;

public interface IClientService
{
    Task<ClientResponseDto> AddIndividualClientAsync(CreateIndividualClientRequestDto request, CancellationToken ct);
    Task<ClientResponseDto> AddCompanyClientAsync(CreateCompanyClientRequestDto request, CancellationToken ct);
    Task UpdateIndividualClientAsync(int id, UpdateIndividualClientRequestDto request, CancellationToken ct);
    Task UpdateCompanyClientAsync(int id, UpdateCompanyClientRequestDto request, CancellationToken ct);
    Task DeleteClientAsync(int id, CancellationToken ct);
}
