using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moba.Api.Auth;
using Moba.Api.Data;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Default'.");
builder.Services.AddDbContext<MobaDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// Passwords: ASP.NET Core Identity's hasher (PBKDF2 with a per-password salt), without the rest of Identity.
builder.Services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();

builder.Services.AddAuthentication(SessionAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>(SessionAuthHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Login and registration: at most 10 attempts per minute per IP address (slows down password guessing).
// Behind a reverse proxy (phase 4) the client IP must come from forwarded headers, or everyone shares one limit.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AccountEndpoints.AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.OnRejected = async (context, token) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ApiError("rate_limited", "Too many attempts. Wait a minute and try again."), token);
});

var app = builder.Build();

// Bring the database schema up to date on startup (creates the tables on first run).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MobaDbContext>();
    db.Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => Results.Ok(new { service = "moba-api", status = "running" }));

// Liveness + database check, for us and later for the server's monitoring.
app.MapGet("/health", async (MobaDbContext db) =>
{
    bool database = await db.Database.CanConnectAsync();
    return database
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
});

app.MapAccountEndpoints();

app.Run();
