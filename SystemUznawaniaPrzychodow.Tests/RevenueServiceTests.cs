using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Tests;

public class RevenueServiceTests
{
    private static RevenueService NewService(AppDbContext db)
        => new(db, new ThrowingHttpClientFactory());

    private static void SeedClientAndSoftware(AppDbContext db, int clientId = 1, int softwareId = 1)
    {
        db.Add(new IndividualClient { Id = clientId, FirstName = "Jan", LastName = "Kowalski",
            Address = "x", Email = "j@x.pl", Phone = "1", Pesel = "90010112345" });
        db.Add(new Software { Id = softwareId, Name = "App", Description = "d",
            CurrentVersion = "1.0", Category = "Finanse", YearlyLicensePrice = 1000m });
    }

    [Fact]
    public async Task Current_SumsSignedContractsAndAllSubscriptionPayments()
    {
        await using var db = TestDb.NewContext();
        SeedClientAndSoftware(db);
        db.Add(new Contract { ClientId = 1, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(10),
            Price = 5000m, IsSigned = true, TotalPaid = 5000m });
        var sub = new Subscription { Name = "S", ClientId = 1, SoftwareId = 1,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = DateTime.UtcNow.Date };
        db.Add(sub);
        await db.SaveChangesAsync();
        db.Add(new SubscriptionPayment { SubscriptionId = sub.Id, Amount = 100m,
            Date = DateTime.UtcNow, PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddMonths(1) });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCurrentRevenueAsync(null, "PLN", CancellationToken.None);

        Assert.Equal(5100m, result.Amount);
        Assert.Equal("PLN", result.Currency);
    }

    [Fact]
    public async Task Current_ExcludesUnsignedContracts()
    {
        await using var db = TestDb.NewContext();
        SeedClientAndSoftware(db);
        db.Add(new Contract { ClientId = 1, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(10),
            Price = 5000m, IsSigned = false, TotalPaid = 2000m });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCurrentRevenueAsync(null, "PLN", CancellationToken.None);

        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public async Task Current_FilteredBySoftware_OnlyCountsThatSoftware()
    {
        await using var db = TestDb.NewContext();
        SeedClientAndSoftware(db, clientId: 1, softwareId: 1);
        db.Add(new Software { Id = 2, Name = "Other", Description = "d",
            CurrentVersion = "1.0", Category = "Edukacja", YearlyLicensePrice = 1000m });
        db.Add(new Contract { ClientId = 1, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(10),
            Price = 5000m, IsSigned = true, TotalPaid = 5000m });
        db.Add(new Contract { ClientId = 1, SoftwareId = 2, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(10),
            Price = 7000m, IsSigned = true, TotalPaid = 7000m });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCurrentRevenueAsync(1, "PLN", CancellationToken.None);

        Assert.Equal(5000m, result.Amount);
    }

    [Fact]
    public async Task Current_FilteredBySoftware_AttributesSubscriptionPaymentsViaNavigation()
    {
        
        await using var db = TestDb.NewContext();
        SeedClientAndSoftware(db, clientId: 1, softwareId: 1);
        db.Add(new Software { Id = 2, Name = "Other", Description = "d",
            CurrentVersion = "1.0", Category = "Edukacja", YearlyLicensePrice = 1000m });
        var subA = new Subscription { Name = "A", ClientId = 1, SoftwareId = 1,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = DateTime.UtcNow.Date };
        var subB = new Subscription { Name = "B", ClientId = 1, SoftwareId = 2,
            RenewalPeriodMonths = 1, PricePerRenewal = 700m, StartDate = DateTime.UtcNow.Date };
        db.AddRange(subA, subB);
        await db.SaveChangesAsync();
        db.Add(new SubscriptionPayment { SubscriptionId = subA.Id, Amount = 100m,
            Date = DateTime.UtcNow, PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddMonths(1) });
        db.Add(new SubscriptionPayment { SubscriptionId = subB.Id, Amount = 700m,
            Date = DateTime.UtcNow, PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddMonths(1) });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetCurrentRevenueAsync(1, "PLN", CancellationToken.None);

        Assert.Equal(100m, result.Amount);
    }

    [Fact]
    public async Task Current_EmptyDatabase_ReturnsZero()
    {
        await using var db = TestDb.NewContext();

        var result = await NewService(db).GetCurrentRevenueAsync(null, "PLN", CancellationToken.None);

        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public async Task Predicted_AddsUnsignedActiveContractsAndOneRenewalPerActiveSubscription()
    {
        await using var db = TestDb.NewContext();
        SeedClientAndSoftware(db);
        var today = DateTime.UtcNow.Date;
        db.Add(new Contract { ClientId = 1, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = today, EndDate = today.AddDays(10), Price = 5000m, IsSigned = true, TotalPaid = 5000m });
        db.Add(new Contract { ClientId = 1, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = today, EndDate = today.AddDays(10), Price = 3000m, IsSigned = false, TotalPaid = 0m });
        var sub = new Subscription { Name = "S", ClientId = 1, SoftwareId = 1,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = today, IsActive = true };
        db.Add(sub);
        await db.SaveChangesAsync();
        db.Add(new SubscriptionPayment { SubscriptionId = sub.Id, Amount = 100m,
            Date = DateTime.UtcNow, PeriodStart = today, PeriodEnd = today.AddMonths(1) });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetPredictedRevenueAsync(null, "PLN", CancellationToken.None);

        Assert.Equal(8200m, result.Amount);
    }

    [Fact]
    public async Task Predicted_ExcludesExpiredUnsignedContracts()
    {
        await using var db = TestDb.NewContext();
        SeedClientAndSoftware(db);
        var today = DateTime.UtcNow.Date;
        db.Add(new Contract { ClientId = 1, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = today.AddDays(-20), EndDate = today.AddDays(-1), Price = 9000m, IsSigned = false, TotalPaid = 0m });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetPredictedRevenueAsync(null, "PLN", CancellationToken.None);

        Assert.Equal(0m, result.Amount);
    }
}
