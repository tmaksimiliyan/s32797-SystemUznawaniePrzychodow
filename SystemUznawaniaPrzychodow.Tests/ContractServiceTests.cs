using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Tests;

public class ContractServiceTests
{
    private static async Task<(int clientId, int softwareId)> SeedClientAndSoftwareAsync(
        AppDbContext db, decimal yearlyPrice = 5000m)
    {
        var client = new IndividualClient
        {
            FirstName = "Jan", LastName = "Kowalski", Address = "ul. Główna 1",
            Email = "jan@example.com", Phone = "500100200", Pesel = "90010112345"
        };
        var software = new Software
        {
            Name = "FinApp", Description = "Finanse", CurrentVersion = "1.0",
            Category = "Finanse", YearlyLicensePrice = yearlyPrice
        };
        db.Add(client);
        db.Add(software);
        await db.SaveChangesAsync();
        return (client.Id, software.Id);
    }

    private static CreateContractRequestDto Req(int clientId, int softwareId,
        int days = 10, int support = 0) =>
        new(clientId, softwareId, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(days), support);

    [Fact]
    public async Task Create_NoDiscountNotReturning_PriceEqualsBase()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 5000m);
        var service = new ContractService(db);

        var result = await service.CreateContractAsync(Req(clientId, softwareId), CancellationToken.None);

        Assert.Equal(5000m, result.Price);
        Assert.False(result.IsSigned);
        Assert.Equal(1, result.TotalSupportYears);
    }

    [Fact]
    public async Task Create_AddsThousandPerSupportYear()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 5000m);
        var service = new ContractService(db);

        var result = await service.CreateContractAsync(Req(clientId, softwareId, support: 3), CancellationToken.None);

        Assert.Equal(8000m, result.Price);
        Assert.Equal(4, result.TotalSupportYears);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(31)]
    public async Task Create_DurationOutOfRange_ThrowsBadRequest(int days)
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var service = new ContractService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateContractAsync(Req(clientId, softwareId, days: days), CancellationToken.None));
    }

    [Fact]
    public async Task Create_TooManySupportYears_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var service = new ContractService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateContractAsync(Req(clientId, softwareId, support: 4), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ClientNotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var (_, softwareId) = await SeedClientAndSoftwareAsync(db);
        var service = new ContractService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateContractAsync(Req(999, softwareId), CancellationToken.None));
    }

    [Fact]
    public async Task Create_SoftwareNotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var (clientId, _) = await SeedClientAndSoftwareAsync(db);
        var service = new ContractService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateContractAsync(Req(clientId, 999), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ReturningClient_AppliesFivePercent()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 5000m);
        db.Add(new Contract
        {
            ClientId = clientId, SoftwareId = 4242, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date.AddDays(-40), EndDate = DateTime.UtcNow.Date.AddDays(-30),
            Price = 1000m, IsSigned = true, TotalPaid = 1000m
        });
        await db.SaveChangesAsync();
        var service = new ContractService(db);

        var result = await service.CreateContractAsync(Req(clientId, softwareId), CancellationToken.None);

        Assert.Equal(4750m, result.Price);
    }

    [Fact]
    public async Task Create_PicksHighestDiscount_AndStacksWithReturning()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db, 5000m);
        var today = DateTime.UtcNow.Date;
        db.AddRange(
            new Discount { Name = "Mała", DiscountType = DiscountType.Contract, Value = 10m,
                DateFrom = today.AddDays(-1), DateTo = today.AddDays(1), SoftwareId = softwareId },
            new Discount { Name = "Duża", DiscountType = DiscountType.Contract, Value = 20m,
                DateFrom = today.AddDays(-1), DateTo = today.AddDays(1), SoftwareId = softwareId },
            new Discount { Name = "Sub", DiscountType = DiscountType.Subscription, Value = 50m,
                DateFrom = today.AddDays(-1), DateTo = today.AddDays(1), SoftwareId = softwareId });
        db.Add(new Subscription { Name = "S", ClientId = clientId, SoftwareId = 4242,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = today.AddDays(-60) });
        await db.SaveChangesAsync();
        var service = new ContractService(db);

        var result = await service.CreateContractAsync(Req(clientId, softwareId), CancellationToken.None);

        Assert.Equal(3800m, result.Price);
    }

    [Fact]
    public async Task Create_WhenActiveSubscriptionExists_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        db.Add(new Subscription { Name = "S", ClientId = clientId, SoftwareId = softwareId,
            RenewalPeriodMonths = 1, PricePerRenewal = 100m, StartDate = DateTime.UtcNow.Date, IsActive = true });
        await db.SaveChangesAsync();
        var service = new ContractService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateContractAsync(Req(clientId, softwareId), CancellationToken.None));
    }

    [Fact]
    public async Task Create_WhenActiveUnsignedContractExists_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        db.Add(new Contract { ClientId = clientId, SoftwareId = softwareId, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(10),
            Price = 5000m, IsSigned = false });
        await db.SaveChangesAsync();
        var service = new ContractService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateContractAsync(Req(clientId, softwareId), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_UnsignedContract_Removes()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        var service = new ContractService(db);
        var created = await service.CreateContractAsync(Req(clientId, softwareId), CancellationToken.None);

        await service.DeleteContractAsync(created.Id, CancellationToken.None);

        Assert.Equal(0, await db.Contracts.CountAsync());
    }

    [Fact]
    public async Task Delete_SignedContract_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var (clientId, softwareId) = await SeedClientAndSoftwareAsync(db);
        db.Add(new Contract { Id = 0, ClientId = clientId, SoftwareId = softwareId, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(10),
            Price = 5000m, IsSigned = true, TotalPaid = 5000m });
        await db.SaveChangesAsync();
        var signedId = await db.Contracts.Select(c => c.Id).SingleAsync();
        var service = new ContractService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteContractAsync(signedId, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_NotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var service = new ContractService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteContractAsync(999, CancellationToken.None));
    }
}
