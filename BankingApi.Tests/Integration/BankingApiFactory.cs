using BankingApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BankingApi.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that replaces PostgreSQL with an in-memory SQLite database.
/// SQLite is used instead of EF Core's InMemory provider because it enforces real SQL
/// constraints (FKs, types) giving higher-fidelity tests. Each factory instance gets
/// its own isolated connection so tests don't share state.
/// </summary>
public class BankingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Keep the connection open for the lifetime of the factory so the :memory: DB persists
    private SqliteConnection? _connection;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();

        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Program.cs skips Npgsql registration in the Testing environment,
            // so we simply register SQLite here with no provider conflict.
            services.AddDbContext<BankingDbContext>(options =>
                options.UseSqlite(_connection!));
        });
    }
}
