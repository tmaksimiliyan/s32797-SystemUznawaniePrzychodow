using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Services;

public class ContractPaymentService(AppDbContext db) : IContractPaymentService
{
    public async Task<ContractPaymentResponseDto> PayAsync(CreateContractPaymentRequestDto request, CancellationToken ct)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.ContractId, ct)
            ?? throw new NotFoundException($"Kontrakt o Id {request.ContractId} nie istnieje.");

        if (contract.IsSigned)
        {
            throw new ConflictException("Kontrakt jest już w pełni opłacony i podpisany.");
        }

        if (DateTime.UtcNow.Date > contract.EndDate.Date)
        {
            throw new BadRequestException("Termin płatności minął. Poprzednie wpłaty zostaną zwrócone. Przygotuj nową ofertę.");
        }

        var clientExists = await db.Clients.AnyAsync(c => c.Id == request.ClientId, ct);

        if (!clientExists)
        {
            throw new NotFoundException($"Klient o Id {request.ClientId} nie istnieje.");
        }

        if (contract.ClientId != request.ClientId)
        {
            throw new BadRequestException("Podany klient nie jest właścicielem tego kontraktu.");
        }

        if (request.Amount <= 0)
        {
            throw new BadRequestException("Kwota płatności musi być większa od zera.");
        }

        var remaining = contract.Price - contract.TotalPaid;

        if (request.Amount > remaining)
        {
            throw new BadRequestException($"Kwota {request.Amount} PLN przekracza pozostałą do zapłaty kwotę {remaining} PLN.");
        }

        var payment = new ContractPayment
        {
            ContractId = contract.Id,
            ClientId = request.ClientId,
            Amount = request.Amount,
            Date = DateTime.UtcNow
        };

        await db.ContractPayments.AddAsync(payment, ct);
        contract.TotalPaid += request.Amount;

        if (contract.TotalPaid == contract.Price)
        {
            contract.IsSigned = true;
        }

        await db.SaveChangesAsync(ct);

        return new ContractPaymentResponseDto(
            payment.Id, contract.Id, request.ClientId,
            payment.Amount, payment.Date,
            contract.TotalPaid, contract.Price, contract.IsSigned);
    }
}
