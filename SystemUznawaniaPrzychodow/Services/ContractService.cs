using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Logic;
using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Services;

public class ContractService(AppDbContext db) : IContractService
{
    public async Task<ContractResponseDto> CreateContractAsync(CreateContractRequestDto request, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new NotFoundException($"Klient o Id {request.ClientId} nie istnieje.");

        if (client is IndividualClient { IsDeleted: true })
        {
            throw new BadRequestException("Nie można tworzyć kontraktu dla usuniętego klienta.");
        }

        var software = await db.Softwares
            .Include(s => s.Discounts)
            .FirstOrDefaultAsync(s => s.Id == request.SoftwareId, ct)
            ?? throw new NotFoundException($"Oprogramowanie o Id {request.SoftwareId} nie istnieje.");

        var duration = (request.EndDate.Date - request.StartDate.Date).Days;

        if (duration < 3 || duration > 30)
        {
            throw new BadRequestException("Przedział czasowy kontraktu musi wynosić od 3 do 30 dni.");
        }

        if (request.AdditionalSupportYears < 0 || request.AdditionalSupportYears > 3)
        {
            throw new BadRequestException("Dodatkowe lata wsparcia muszą wynosić 0, 1, 2 lub 3.");
        }

        var hasActiveSubscription = await db.Subscriptions
            .AnyAsync(s => s.ClientId == request.ClientId && s.SoftwareId == request.SoftwareId && s.IsActive, ct);

        if (hasActiveSubscription)
        {
            throw new ConflictException("Klient ma już aktywną subskrypcję na to oprogramowanie.");
        }

        var hasActiveContract = await db.Contracts
            .AnyAsync(c => c.ClientId == request.ClientId && c.SoftwareId == request.SoftwareId && !c.IsSigned && c.EndDate >= DateTime.UtcNow.Date, ct);

        if (hasActiveContract)
        {
            throw new ConflictException("Klient ma już aktywny kontrakt na to oprogramowanie.");
        }

        var today = DateTime.UtcNow.Date;
        var basePrice = PriceCalculator.CalculateBaseContractPrice(software.YearlyLicensePrice, request.AdditionalSupportYears);
        var bestDiscount = PriceCalculator.GetBestContractDiscount(software.Discounts, today);
        var isReturningClient = await db.Subscriptions.AnyAsync(s => s.ClientId == request.ClientId, ct)
            || await db.Contracts.AnyAsync(c => c.ClientId == request.ClientId && c.IsSigned, ct);
        var price = PriceCalculator.CalculateFinalContractPrice(basePrice, bestDiscount, isReturningClient);

        var contract = new Contract
        {
            ClientId = request.ClientId,
            SoftwareId = request.SoftwareId,
            SoftwareVersion = software.CurrentVersion,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Price = price,
            AdditionalSupportYears = request.AdditionalSupportYears
        };

        await db.Contracts.AddAsync(contract, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(contract, software);
    }

    public async Task DeleteContractAsync(int id, CancellationToken ct)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Kontrakt o Id {id} nie istnieje.");

        if (contract.IsSigned)
        {
            throw new ConflictException("Nie można usunąć podpisanej umowy.");
        }

        db.Contracts.Remove(contract);
        await db.SaveChangesAsync(ct);
    }

    private static ContractResponseDto ToResponse(Contract c, Software s) => new(
        c.Id, c.ClientId, c.SoftwareId, s.Name, c.SoftwareVersion,
        c.StartDate, c.EndDate, c.Price, 1 + c.AdditionalSupportYears, c.IsSigned, c.TotalPaid);
}
