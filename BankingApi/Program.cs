using BankingApi.Data;
using BankingApi.Repositories;
using BankingApi.Repositories.Contracts;
using BankingApi.Services;
using BankingApi.Services.Contracts;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database — PostgreSQL via EF Core (Npgsql).
// Skipped in the Testing environment: the WebApplicationFactory registers
// an in-memory SQLite context instead, keeping tests self-contained and
// avoiding a "two database providers registered" conflict.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<BankingDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Repositories (Repository pattern)
builder.Services.AddScoped<ICardRepository, CardRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Services (Facade pattern over repos and FX adapter)
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// FX Service — Adapter over the US Treasury Reporting Rates of Exchange API
builder.Services.AddHttpClient<IFxService, TreasuryFxService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TreasuryApi:BaseUrl"]
        ?? "https://api.fiscaldata.treasury.gov/services/api/fiscal_service");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Banking API", Version = "v1" });
});

var app = builder.Build();

// Apply schema on startup. Skipped in the Testing environment because the
// WebApplicationFactory creates the schema via its own SQLite connection.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Banking API v1");
    options.RoutePrefix = "swagger";
});

app.MapOpenApi();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program to integration tests
public partial class Program { }
