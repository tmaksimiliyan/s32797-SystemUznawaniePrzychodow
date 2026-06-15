using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Models;
using SystemUznawaniaPrzychodow.Services;

namespace SystemUznawaniaPrzychodow.Tests;

public class ClientServiceTests
{
    private static CreateIndividualClientRequestDto IndividualReq(string pesel = "90010112345") =>
        new("Jan", "Kowalski", "ul. Główna 1", "jan@example.com", "500100200", pesel);

    private static CreateCompanyClientRequestDto CompanyReq(string krs = "0000123456") =>
        new("ABC Sp. z o.o.", "ul. Firmowa 2", "biuro@abc.pl", "224004000", krs);

    [Fact]
    public async Task AddIndividual_NewPesel_PersistsClient()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);

        var result = await service.AddIndividualClientAsync(IndividualReq(), CancellationToken.None);

        Assert.Equal("Individual", result.Type);
        Assert.Equal("90010112345", result.Pesel);
        Assert.Equal(1, await db.IndividualClients.CountAsync());
    }

    [Fact]
    public async Task AddIndividual_DuplicatePesel_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        await service.AddIndividualClientAsync(IndividualReq("11111111111"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddIndividualClientAsync(IndividualReq("11111111111"), CancellationToken.None));
    }

    [Fact]
    public async Task AddCompany_DuplicateKrs_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        await service.AddCompanyClientAsync(CompanyReq("0000999999"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddCompanyClientAsync(CompanyReq("0000999999"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateIndividual_ChangesContactData_ButNotPesel()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddIndividualClientAsync(IndividualReq("22222222222"), CancellationToken.None);

        await service.UpdateIndividualClientAsync(created.Id,
            new UpdateIndividualClientRequestDto("Anna", "Nowak", "ul. Inna 5", "anna@x.pl", "600700800"),
            CancellationToken.None);

        var entity = await db.IndividualClients.SingleAsync();
        Assert.Equal("Anna", entity.FirstName);
        Assert.Equal("Nowak", entity.LastName);
        Assert.Equal("22222222222", entity.Pesel);
    }

    [Fact]
    public async Task UpdateIndividual_NotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateIndividualClientAsync(999,
                new UpdateIndividualClientRequestDto("A", "B", "C", "d@e.pl", "1"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateIndividual_SoftDeleted_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddIndividualClientAsync(IndividualReq("33333333333"), CancellationToken.None);
        await service.DeleteClientAsync(created.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateIndividualClientAsync(created.Id,
                new UpdateIndividualClientRequestDto("A", "B", "C", "d@e.pl", "1"), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCompany_ChangesData_ButNotKrs()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddCompanyClientAsync(CompanyReq("0000111222"), CancellationToken.None);

        await service.UpdateCompanyClientAsync(created.Id,
            new UpdateCompanyClientRequestDto("Nowa Nazwa", "ul. Nowa 9", "new@abc.pl", "111222333"),
            CancellationToken.None);

        var entity = await db.CompanyClients.SingleAsync();
        Assert.Equal("Nowa Nazwa", entity.CompanyName);
        Assert.Equal("0000111222", entity.Krs);
    }

    [Fact]
    public async Task DeleteIndividual_SoftDeletes_OverwritesData_AndSetsFlag()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddIndividualClientAsync(IndividualReq("44444444444"), CancellationToken.None);

        await service.DeleteClientAsync(created.Id, CancellationToken.None);

        var entity = await db.IndividualClients.SingleAsync();
        Assert.True(entity.IsDeleted);
        Assert.Equal("[usunięty]", entity.FirstName);
        Assert.NotEqual("44444444444", entity.Pesel);
    }

    [Fact]
    public async Task DeleteIndividual_PeselMarkerFitsColumnLimit()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddIndividualClientAsync(IndividualReq("55555555555"), CancellationToken.None);

        await service.DeleteClientAsync(created.Id, CancellationToken.None);

        var entity = await db.IndividualClients.SingleAsync();
        Assert.True(entity.Pesel.Length <= 11,
            $"Marker PESEL '{entity.Pesel}' ma {entity.Pesel.Length} znaków, a limit kolumny to 11.");
    }

    [Fact]
    public async Task DeleteIndividual_AlreadyDeleted_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddIndividualClientAsync(IndividualReq("66666666666"), CancellationToken.None);
        await service.DeleteClientAsync(created.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteClientAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteCompany_ThrowsConflict()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);
        var created = await service.AddCompanyClientAsync(CompanyReq("0000777888"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.DeleteClientAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteClient_NotFound_ThrowsNotFound()
    {
        await using var db = TestDb.NewContext();
        var service = new ClientService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteClientAsync(12345, CancellationToken.None));
    }
}
