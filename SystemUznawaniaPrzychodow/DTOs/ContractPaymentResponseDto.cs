namespace SystemUznawaniaPrzychodow.DTOs;

public record ContractPaymentResponseDto(
    int Id,
    int ContractId,
    int ClientId,
    decimal Amount,
    DateTime Date,
    decimal TotalPaid,
    decimal ContractPrice,
    bool ContractSigned);
