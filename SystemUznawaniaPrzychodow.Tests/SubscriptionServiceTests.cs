using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Tests;

public class SubscriptionServiceTests
{
    private static async Task<(int clientId, int softwareId)> SeedClientAndSoftwareAsync(
        AppDbContext db, decimal yearlyPrice = 1200m)
    {
        var client = new IndividualClient
        {
            FirstName = "Jan", LastName = "Kowalski", Address = "ul. Główna 1",
            Email = "jan@example.com", Phone = "500100200", Pesel = "90010112345"
        };
        var software = new Software
        {
            Name = "EduApp", Description = "Edukacja", CurrentVersion = "2.0",
            Category = "Edukacja", YearlyLicensePrice = yearlyPrice
        };
        db.Add(client);
        db.Add(software);
        await db.SaveChangesAsync();
        return (client.Id, software.Id);
    }


    [Fact]
    public async Task Create_NewClientNoPromo_FirstPeriodFull_RenewalGetsLoyalty()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 1200m);
        var service = new SubscriptionService(db);

        var result = await service.CreateSubscriptionAsync(
            new CreateSubscriptionRequestDto(clientId, softwareId, "Plan", 1), CancellationToken.None);

        var firstPayment = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal(100m, firstPayment.Amount);
        Assert.Equal(95m, result.PricePerRenewal);
    }

    [Fact]
    public async Task Create_LoyalClient_AppliesFivePercentToFirstAndRenewal()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 1200m);
        // Lojalny: ma podpisany kontrakt.
        db.Add(new Contract { ClientId = clientId, SoftwareId = 4242, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date.AddDays(-40), EndDate = DateTime.UtcNow.Date.AddDays(-30),
            Price = 500m, IsSigned = true, TotalPaid = 500m });
        await db.SaveChangesAsync();
        var service = new SubscriptionService(db);

        var result = await service.CreateSubscriptionAsync(
            new CreateSubscriptionRequestDto(clientId, softwareId, "Plan", 1), CancellationToken.None);

        Assert.Equal(95m, result.PricePerRenewal); // 100 * 0.95
        var firstPayment = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal(95m, firstPayment.Amount);
    }

    [Fact]
    public async Task Create_WithPromo_DiscountsFirstPeriodOnly_NotRenewal()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 1200m);
        var today = DateTime.UtcNow.Date;
        db.Add(new Discount { Name = "Promo", DiscountType = DiscountType.Subscription, Value = 10m,
            DateFrom = today.AddDays(-1), DateTo = today.AddDays(1), SoftwareId = softwareId });
        await db.SaveChangesAsync();
        var service = new SubscriptionService(db);

        var result = await service.CreateSubscriptionAsync(
            new CreateSubscriptionRequestDto(clientId, softwareId, "Plan", 1), CancellationToken.None);

       
        Assert.Equal(95m, result.PricePerRenewal);
        var firstPayment = await db.SubscriptionPayments.SingleAsync();
        Assert.Equal(90m, firstPayment.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task Create_RenewalPeriodOutOfRange_ThrowsBadRequest(int months)
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateSubscriptionAsync(
                new CreateSubscriptionRequestDto(clientId, softwareId, "Plan", months), CancellationToken.None));
    }

    [Fact]
    public async Task Create_WhenActiveSubscriptionExists_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        db.Add(new Subscription { Name = "Old", ClientId = clientId, SoftwareId = softwareId,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = DateTime.UtcNow.Date, IsActive = true });
        await db.SaveChangesAsync();
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateSubscriptionAsync(
                new CreateSubscriptionRequestDto(clientId, softwareId, "Plan", 1), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ClientNotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var (_, softwareId) = await SeedClientAndSoftwareAsync(db);
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateSubscriptionAsync(
                new CreateSubscriptionRequestDto(999, softwareId, "Plan", 1), CancellationToken.None));
    }
    
    private static async Task<int> SeedSubscriptionWithFirstPaymentAsync(
        AppDbContext db, int clientId, int softwareId, DateTime periodStart, int months = 1, decimal renewal = 100m)
    {
        var sub = new Subscription
        {
            Name = "Plan", ClientId = clientId, SoftwareId = softwareId,
            RenewalPeriodMonths = months, PricePerRenewal = renewal,
            StartDate = periodStart, IsActive = true
        };
        db.Add(sub);
        await db.SaveChangesAsync();

        db.Add(new SubscriptionPayment
        {
            SubscriptionId = sub.Id, Amount = renewal, Date = periodStart,
            PeriodStart = periodStart, PeriodEnd = periodStart.AddMonths(months)
        });
        await db.SaveChangesAsync();
        return sub.Id;
    }

    [Fact]
    public async Task Renew_ValidAtPeriodStart_AddsPayment()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var today = DateTime.UtcNow.Date;
        var subId = await SeedSubscriptionWithFirstPaymentAsync(db, clientId, softwareId, today.AddMonths(-1));
        var service = new SubscriptionService(db);

        var result = await service.RenewAsync(new RenewSubscriptionRequestDto(subId, clientId, 100m), CancellationToken.None);

        Assert.Equal(100m, result.Amount);
        Assert.Equal(2, await db.SubscriptionPayments.CountAsync());
    }

    [Fact]
    public async Task Renew_WrongAmount_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var today = DateTime.UtcNow.Date;
        var subId = await SeedSubscriptionWithFirstPaymentAsync(db, clientId, softwareId, today.AddMonths(-1));
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.RenewAsync(new RenewSubscriptionRequestDto(subId, clientId, 99m), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_BeforeNextPeriodStarts_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var today = DateTime.UtcNow.Date;
        var subId = await SeedSubscriptionWithFirstPaymentAsync(db, clientId, softwareId, today.AddDays(10).AddMonths(-1));
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.RenewAsync(new RenewSubscriptionRequestDto(subId, clientId, 100m), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_OverdueUnpaidPeriod_CancelsSubscription()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var today = DateTime.UtcNow.Date;
        var subId = await SeedSubscriptionWithFirstPaymentAsync(db, clientId, softwareId, today.AddMonths(-3));
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RenewAsync(new RenewSubscriptionRequestDto(subId, clientId, 100m), CancellationToken.None));

        var sub = await db.Subscriptions.SingleAsync();
        Assert.False(sub.IsActive);
    }

    [Fact]
    public async Task Renew_NotOwner_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var today = DateTime.UtcNow.Date;
        var subId = await SeedSubscriptionWithFirstPaymentAsync(db, clientId, softwareId, today.AddMonths(-1));
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.RenewAsync(new RenewSubscriptionRequestDto(subId, 99999, 100m), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_InactiveSubscription_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var sub = new Subscription { Name = "Plan", ClientId = clientId, SoftwareId = softwareId,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = DateTime.UtcNow.Date.AddMonths(-2), IsActive = false };
        db.Add(sub);
        await db.SaveChangesAsync();
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RenewAsync(new RenewSubscriptionRequestDto(sub.Id, clientId, 100m), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_NotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var service = new SubscriptionService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RenewAsync(new RenewSubscriptionRequestDto(999, 1, 100m), CancellationToken.None));
    }
}
