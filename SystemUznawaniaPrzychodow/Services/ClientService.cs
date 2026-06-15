using Microsoft.EntityFrameworkCore;
using SystemUznawaniaPrzychodow.Data;
using SystemUznawaniaPrzychodow.DTOs;
using SystemUznawaniaPrzychodow.Exceptions;
using SystemUznawaniaPrzychodow.Models;

namespace SystemUznawaniaPrzychodow.Services;

public class ClientService(AppDbContext db) : IClientService
{
    public async Task<ClientResponseDto> AddIndividualClientAsync(CreateIndividualClientRequestDto request, CancellationToken ct)
    {
        if (await db.IndividualClients.AnyAsync(c => c.Pesel == request.Pesel, ct))
        {
            throw new ConflictException($"Klient z PESEL {request.Pesel} już istnieje.");
        }

        var client = new IndividualClient
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Address = request.Address,
            Email = request.Email,
            Phone = request.Phone,
            Pesel = request.Pesel
        };

        await db.IndividualClients.AddAsync(client, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(client);
    }

    public async Task<ClientResponseDto> AddCompanyClientAsync(CreateCompanyClientRequestDto request, CancellationToken ct)
    {
        if (await db.CompanyClients.AnyAsync(c => c.Krs == request.Krs, ct))
        {
            throw new ConflictException($"Firma z KRS {request.Krs} już istnieje.");
        }

        var client = new CompanyClient
        {
            CompanyName = request.CompanyName,
            Address = request.Address,
            Email = request.Email,
            Phone = request.Phone,
            Krs = request.Krs
        };

        await db.CompanyClients.AddAsync(client, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(client);
    }

    public async Task UpdateIndividualClientAsync(int id, UpdateIndividualClientRequestDto request, CancellationToken ct)
    {
        var client = await db.IndividualClients
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct)
            ?? throw new NotFoundException($"Osoba fizyczna o Id {id} nie istnieje lub została usunięta.");

        client.FirstName = request.FirstName;
        client.LastName = request.LastName;
        client.Address = request.Address;
        client.Email = request.Email;
        client.Phone = request.Phone;

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateCompanyClientAsync(int id, UpdateCompanyClientRequestDto request, CancellationToken ct)
    {
        var client = await db.CompanyClients
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException($"Firma o Id {id} nie istnieje.");

        client.CompanyName = request.CompanyName;
        client.Address = request.Address;
        client.Email = request.Email;
        client.Phone = request.Phone;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteClientAsync(int id, CancellationToken ct)
    {
        var individual = await db.IndividualClients.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (individual is not null)
        {
            if (individual.IsDeleted)
            {
                throw new ConflictException($"Klient o Id {id} jest już usunięty.");
            }

            individual.IsDeleted = true;
            individual.FirstName = "[usunięty]";
            individual.LastName = "[usunięty]";
            individual.Email = "[usunięty]";
            individual.Phone = "[usunięty]";
            individual.Address = "[usunięty]";
            individual.Pesel = "[usunięty]";

            await db.SaveChangesAsync(ct);
            return;
        }

        var company = await db.CompanyClients.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (company is not null)
        {
            throw new ConflictException("Danych firm nie można usuwać.");
        }

        throw new NotFoundException($"Klient o Id {id} nie istnieje.");
    }

    private static ClientResponseDto ToResponse(Client client) => client switch
    {
        IndividualClient i => new ClientResponseDto(i.Id, "Individual", i.Address, i.Email, i.Phone, i.FirstName, i.LastName, i.Pesel, null, null),
        CompanyClient c => new ClientResponseDto(c.Id, "Company", c.Address, c.Email, c.Phone, null, null, null, c.CompanyName, c.Krs),
        _ => throw new InvalidOperationException("Nieznany typ klienta.")
    };
}
