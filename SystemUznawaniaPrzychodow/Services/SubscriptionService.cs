using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Logic;
using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Services;

public class SubscriptionService(AppDbContext db) : ISubscriptionService
{
    public async Task<SubscriptionResponseDto> CreateSubscriptionAsync(CreateSubscriptionRequestDto request, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new NotFoundException($"Klient o Id {request.ClientId} nie istnieje.");

        if (client is IndividualClient { IsDeleted: true })
        {
            throw new BadRequestException("Nie można tworzyć subskrypcji dla usuniętego klienta.");
        }

        var software = await db.Softwares
            .Include(s => s.Discounts)
            .FirstOrDefaultAsync(s => s.Id == request.SoftwareId, ct)
            ?? throw new NotFoundException($"Oprogramowanie o Id {request.SoftwareId} nie istnieje.");

        if (request.RenewalPeriodMonths < 1 || request.RenewalPeriodMonths > 24)
        {
            throw new BadRequestException("Okres odnowienia musi wynosić od 1 do 24 miesięcy.");
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
            throw new ConflictException("Klient ma aktywny kontrakt na to oprogramowanie.");
        }

        var today = DateTime.UtcNow.Date;
        var bestPromoDiscount = PriceCalculator.GetBestSubscriptionDiscount(software.Discounts, today);

        var isLoyal = await db.Subscriptions.AnyAsync(s => s.ClientId == request.ClientId, ct)
            || await db.Contracts.AnyAsync(c => c.ClientId == request.ClientId && c.IsSigned, ct);

        var firstPeriodPrice = PriceCalculator.CalculateFirstPeriodSubscriptionPrice(
            software.YearlyLicensePrice, request.RenewalPeriodMonths, bestPromoDiscount, isLoyal);
        
        var renewalPrice = PriceCalculator.CalculateRenewalPrice(
            software.YearlyLicensePrice, request.RenewalPeriodMonths, isLoyalClient: true);

        var subscription = new Subscription
        {
            Name = request.Name,
            ClientId = request.ClientId,
            SoftwareId = request.SoftwareId,
            RenewalPeriodMonths = request.RenewalPeriodMonths,
            PricePerRenewal = renewalPrice,
            StartDate = today
        };

        await db.Subscriptions.AddAsync(subscription, ct);
        await db.SaveChangesAsync(ct);

        var firstPayment = new SubscriptionPayment
        {
            SubscriptionId = subscription.Id,
            Amount = firstPeriodPrice,
            Date = DateTime.UtcNow,
            PeriodStart = today,
            PeriodEnd = today.AddMonths(request.RenewalPeriodMonths)
        };

        await db.SubscriptionPayments.AddAsync(firstPayment, ct);
        await db.SaveChangesAsync(ct);

        return new SubscriptionResponseDto(
            subscription.Id, subscription.Name, subscription.ClientId, subscription.SoftwareId,
            software.Name, subscription.RenewalPeriodMonths, subscription.PricePerRenewal,
            subscription.StartDate, subscription.IsActive);
    }

    public async Task<SubscriptionPaymentResponseDto> RenewAsync(RenewSubscriptionRequestDto request, CancellationToken ct)
    {
        var subscription = await db.Subscriptions
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId, ct)
            ?? throw new NotFoundException($"Subskrypcja o Id {request.SubscriptionId} nie istnieje.");

        if (!subscription.IsActive)
        {
            throw new ConflictException("Subskrypcja jest anulowana.");
        }

        if (subscription.ClientId != request.ClientId)
        {
            throw new BadRequestException("Podany klient nie jest właścicielem tej subskrypcji.");
        }

        var lastPayment = subscription.Payments
            .OrderByDescending(p => p.PeriodEnd)
            .FirstOrDefault()
            ?? throw new BadRequestException("Brak historii płatności dla tej subskrypcji.");

        var today = DateTime.UtcNow.Date;
        var nextPeriodStart = lastPayment.PeriodEnd.Date;
        var nextPeriodEnd = nextPeriodStart.AddMonths(subscription.RenewalPeriodMonths);

        if (today < nextPeriodStart)
        {
            throw new BadRequestException($"Następny okres zaczyna się {nextPeriodStart:yyyy-MM-dd}. Płatność można przyjąć dopiero wtedy.");
        }

        if (today > nextPeriodEnd)
        {
            subscription.IsActive = false;
            await db.SaveChangesAsync(ct);
            throw new ConflictException("Subskrypcja została anulowana z powodu braku płatności za poprzedni okres.");
        }

        var alreadyPaidCurrentPeriod = subscription.Payments.Any(p => p.PeriodStart.Date == nextPeriodStart);

        if (alreadyPaidCurrentPeriod)
        {
            throw new ConflictException("Bieżący okres jest już opłacony.");
        }

        if (request.Amount != subscription.PricePerRenewal)
        {
            throw new BadRequestException($"Nieprawidłowa kwota. Oczekiwana: {subscription.PricePerRenewal} PLN.");
        }

        var payment = new SubscriptionPayment
        {
            SubscriptionId = subscription.Id,
            Amount = request.Amount,
            Date = DateTime.UtcNow,
            PeriodStart = nextPeriodStart,
            PeriodEnd = nextPeriodEnd
        };

        await db.SubscriptionPayments.AddAsync(payment, ct);
        await db.SaveChangesAsync(ct);

        return new SubscriptionPaymentResponseDto(
            payment.Id, payment.SubscriptionId, payment.Amount,
            payment.Date, payment.PeriodStart, payment.PeriodEnd);
    }
}
