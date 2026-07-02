using BankingApi.Services;
using BankingApi.Services.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register services - singleton so in-memory state persists across requests
builder.Services.AddSingleton<ICreditCardService, CreditCardService>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();

// FX service uses HttpClient - registered as transient via the typed client factory
builder.Services.AddHttpClient<IFxService, FxService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Serve Swagger UI at /swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Banking API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
