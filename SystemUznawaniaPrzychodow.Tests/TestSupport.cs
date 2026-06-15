using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SystemUznawaniaPrzychodow.Data;

namespace SystemUznawaniaPrzychodow.Tests;


public static class TestDb
{
    public static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}

public class ThrowingHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
        => throw new InvalidOperationException("Dla waluty PLN nie powinno być wywołania HTTP do NBP.");
}
