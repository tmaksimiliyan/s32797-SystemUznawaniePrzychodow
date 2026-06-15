using SystemUznawaniaPrzychodow.DTOs;

namespace SystemUznawaniaPrzychodow.Services;

public interface IContractService
{
    Task<ContractResponseDto> CreateContractAsync(CreateContractRequestDto request, CancellationToken ct);
    Task DeleteContractAsync(int id, CancellationToken ct);
}
