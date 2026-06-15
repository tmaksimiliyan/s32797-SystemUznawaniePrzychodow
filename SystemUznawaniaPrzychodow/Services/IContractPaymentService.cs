using SystemUznawaniaPrzychodow.DTOs;

namespace SystemUznawaniaPrzychodow.Services;

public interface IContractPaymentService
{
    Task<ContractPaymentResponseDto> PayAsync(CreateContractPaymentRequestDto request, CancellationToken ct);
}
