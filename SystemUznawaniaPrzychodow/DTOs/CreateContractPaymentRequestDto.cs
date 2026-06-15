namespace SystemUznawaniaPrzychodow.DTOs;

public record CreateContractPaymentRequestDto(
    int ContractId,
    int ClientId,
    decimal Amount);
