using Microsoft.EntityFrameworkCore;
using Moba.Api.Data;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Default'.");
builder.Services.AddDbContext<MobaDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

var app = builder.Build();

// Bring the database schema up to date on startup (creates the tables on first run).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MobaDbContext>();
    db.Database.Migrate();
}

app.MapGet("/", () => Results.Ok(new { service = "moba-api", status = "running" }));

// Liveness + database check, for us and later for the server's monitoring.
app.MapGet("/health", async (MobaDbContext db) =>
{
    bool database = await db.Database.CanConnectAsync();
    return database
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
});

app.Run();
