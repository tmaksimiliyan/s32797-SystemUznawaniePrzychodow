using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Tests;

public class ContractPaymentServiceTests
{
    private static async Task<(int clientId, int contractId)> SeedAsync(
        AppDbContext db, decimal price = 1000m, int endInDays = 10, bool signed = false)
    {
        var client = new IndividualClient
        {
            FirstName = "Jan", LastName = "Kowalski", Address = "ul. Główna 1",
            Email = "jan@example.com", Phone = "500100200", Pesel = "90010112345"
        };
        db.Add(client);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ClientId = client.Id, SoftwareId = 1, SoftwareVersion = "1.0",
            StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(endInDays),
            Price = price, IsSigned = signed, TotalPaid = signed ? price : 0m
        };
        db.Add(contract);
        await db.SaveChangesAsync();
        return (client.Id, contract.Id);
    }

    [Fact]
    public async Task Pay_Partial_AccumulatesAndStaysUnsigned()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m);
        var service = new ContractPaymentService(db);

        var result = await service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 400m), CancellationToken.None);

        Assert.Equal(400m, result.TotalPaid);
        Assert.False(result.ContractSigned);
    }

    [Fact]
    public async Task Pay_FullInOneGo_SignsContract()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m);
        var service = new ContractPaymentService(db);

        var result = await service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 1000m), CancellationToken.None);

        Assert.True(result.ContractSigned);
        Assert.Equal(1000m, result.TotalPaid);
    }

    [Fact]
    public async Task Pay_TwoInstallments_ReachFull_Signs()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m);
        var service = new ContractPaymentService(db);

        await service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 600m), CancellationToken.None);
        var second = await service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 400m), CancellationToken.None);

        Assert.True(second.ContractSigned);
        Assert.Equal(2, await db.ContractPayments.CountAsync());
    }

    [Fact]
    public async Task Pay_Overpayment_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m);
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 1500m), CancellationToken.None));
    }

    [Fact]
    public async Task Pay_NonPositiveAmount_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m);
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 0m), CancellationToken.None));
    }

    [Fact]
    public async Task Pay_AfterDeadline_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m, endInDays: -1);
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 500m), CancellationToken.None));
    }

    [Fact]
    public async Task Pay_AlreadySigned_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var (clientId, contractId) = await SeedAsync(db, price: 1000m, signed: true);
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(contractId, clientId, 100m), CancellationToken.None));
    }

    [Fact]
    public async Task Pay_WrongClient_ThrowsBadRequest()
    {
        await using var db = TestDb.NewContext();
        var (_, contractId) = await SeedAsync(db, price: 1000m);
        var other = new IndividualClient { FirstName = "Inny", LastName = "Klient", Address = "x",
            Email = "i@x.pl", Phone = "1", Pesel = "00000000000" };
        db.Add(other);
        await db.SaveChangesAsync();
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(contractId, other.Id, 500m), CancellationToken.None));
    }

    [Fact]
    public async Task Pay_ClientNotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var (_, contractId) = await SeedAsync(db, price: 1000m);
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(contractId, 99999, 500m), CancellationToken.None));
    }

    [Fact]
    public async Task Pay_ContractNotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var service = new ContractPaymentService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.PayAsync(new CreateContractPaymentRequestDto(99999, 1, 500m), CancellationToken.None));
    }
}
