using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;

namespace SystemUznawaniaPrzychodow.Services;

public class RevenueService(AppDbContext db, IHttpClientFactory httpClientFactory) : IRevenueService
{
    public async Task<RevenueResponseDto> GetCurrentRevenueAsync(int? softwareId, string? currency, CancellationToken ct)
    {
        var contractsQuery = db.Contracts.Where(c => c.IsSigned);

        if (softwareId.HasValue)
        {
            contractsQuery = contractsQuery.Where(c => c.SoftwareId == softwareId.Value);
        }

        var contractRevenue = await contractsQuery.SumAsync(c => c.Price, ct);

        var paymentsQuery = db.SubscriptionPayments.AsQueryable();

        if (softwareId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Subscription.SoftwareId == softwareId.Value);
        }

        var subscriptionRevenue = await paymentsQuery.SumAsync(p => p.Amount, ct);

        var total = contractRevenue + subscriptionRevenue;
        return new RevenueResponseDto(await ConvertAsync(total, currency, ct), currency?.ToUpper() ?? "PLN");
    }

    public async Task<RevenueResponseDto> GetPredictedRevenueAsync(int? softwareId, string? currency, CancellationToken ct)
    {
        var currentResponse = await GetCurrentRevenueAsync(softwareId, "PLN", ct);
        var total = currentResponse.Amount;

        var today = DateTime.UtcNow.Date;

        var activeContractsQuery = db.Contracts.Where(c => !c.IsSigned && c.EndDate >= today);

        if (softwareId.HasValue)
        {
            activeContractsQuery = activeContractsQuery.Where(c => c.SoftwareId == softwareId.Value);
        }

        total += await activeContractsQuery.SumAsync(c => c.Price, ct);

        var activeSubscriptionsQuery = db.Subscriptions.Where(s => s.IsActive);

        if (softwareId.HasValue)
        {
            activeSubscriptionsQuery = activeSubscriptionsQuery.Where(s => s.SoftwareId == softwareId.Value);
        }

        total += await activeSubscriptionsQuery.SumAsync(s => s.PricePerRenewal, ct);

        return new RevenueResponseDto(await ConvertAsync(total, currency, ct), currency?.ToUpper() ?? "PLN");
    }

    private async Task<decimal> ConvertAsync(decimal amountPln, string? currency, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.ToUpper() == "PLN")
        {
            return Math.Round(amountPln, 2);
        }

        var client = httpClientFactory.CreateClient("NBP");
        var url = $"https://api.nbp.pl/api/exchangerates/rates/a/{currency.ToLower()}/?format=json";

        HttpResponseMessage response;

        try
        {
            response = await client.GetAsync(url, ct);
        }
        catch
        {
            throw new BadRequestException($"Nie można pobrać kursu waluty {currency.ToUpper()} z NBP.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Nieznana waluta: {currency.ToUpper()}. Sprawdź kod waluty (np. USD, EUR, GBP).");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var rate = doc.RootElement.GetProperty("rates")[0].GetProperty("mid").GetDecimal();
        return Math.Round(amountPln / rate, 2);
    }
}
