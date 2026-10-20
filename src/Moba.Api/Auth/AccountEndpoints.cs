using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moba.Api.Data;
using Npgsql;

namespace Moba.Api.Auth;

// POST /auth/register, POST /auth/login, POST /auth/logout, GET /me, PUT /me/settings
public static class AccountEndpoints
{
    public const string AuthRateLimit = "auth";
    // Upper bound for the settings blob (keybinds, HUD sizes, loadouts...).
    const int MaxSettingsBytes = 16 * 1024;

    static readonly ApiError InvalidCredentials =
        new("invalid_credentials", "Wrong email/username or password.");

    public static void MapAccountEndpoints(this WebApplication app)
    {
        RouteGroupBuilder auth = app.MapGroup("/auth");
        auth.MapPost("/register", Register).RequireRateLimiting(AuthRateLimit);
        auth.MapPost("/login", Login).RequireRateLimiting(AuthRateLimit);
        auth.MapPost("/logout", Logout).RequireAuthorization();

        RouteGroupBuilder me = app.MapGroup("/me").RequireAuthorization();
        me.MapGet("/", GetProfile);
        me.MapPut("/settings", PutSettings);
    }

    static async Task<IResult> Register(RegisterRequest request, MobaDbContext db, IPasswordHasher<Account> hasher)
    {
        string? problem = AccountRules.CheckEmail(request.Email)
                          ?? AccountRules.CheckUsername(request.Username)
                          ?? AccountRules.CheckPassword(request.Password);
        if (problem != null) return Results.BadRequest(new ApiError("invalid_input", problem));

        string email = request.Email!.Trim();
        string username = request.Username!.Trim();
        string normalizedEmail = AccountRules.NormalizeEmail(email);
        string normalizedUsername = AccountRules.NormalizeUsername(username);

        if (await db.Accounts.AnyAsync(a => a.NormalizedEmail == normalizedEmail)) return EmailTaken();
        if (await db.Accounts.AnyAsync(a => a.NormalizedUsername == normalizedUsername)) return UsernameTaken();

        var account = new Account
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = "",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
        };
        account.PasswordHash = hasher.HashPassword(account, request.Password!);
        account.Profile = new PlayerProfile { UpdatedAt = DateTime.UtcNow };
        db.Accounts.Add(account);
        string token = AddSession(db, account.Id, out DateTime expiresAt);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // Someone registered the same name in the moment between the check and the insert.
            return pg.ConstraintName?.Contains("email") == true ? EmailTaken() : UsernameTaken();
        }

        return Results.Created("/me", new AuthResponse(token, expiresAt, ToProfile(account, account.Profile)));
    }

    static async Task<IResult> Login(LoginRequest request, MobaDbContext db, IPasswordHasher<Account> hasher)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
            return Results.BadRequest(new ApiError("invalid_input", "Enter your email or username and your password."));

        string login = request.Login.Trim().ToLowerInvariant();
        IQueryable<Account> accounts = db.Accounts.Include(a => a.Profile);
        Account? account = login.Contains('@')
            ? await accounts.FirstOrDefaultAsync(a => a.NormalizedEmail == login)
            : await accounts.FirstOrDefaultAsync(a => a.NormalizedUsername == login);

        if (account == null)
        {
            // Hash anyway, so a wrong username takes as long as a wrong password (no account probing by timing).
            hasher.HashPassword(new Account { Email = "", NormalizedEmail = "", Username = "", NormalizedUsername = "", PasswordHash = "" }, request.Password);
            return Results.Json(InvalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
        }

        PasswordVerificationResult result = hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Results.Json(InvalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            account.PasswordHash = hasher.HashPassword(account, request.Password);

        account.LastLoginAt = DateTime.UtcNow;
        account.Profile ??= new PlayerProfile { UpdatedAt = DateTime.UtcNow };
        // Expired sessions of this account are dropped whenever it logs in again.
        await db.Sessions.Where(s => s.AccountId == account.Id && s.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync();
        string token = AddSession(db, account.Id, out DateTime expiresAt);
        await db.SaveChangesAsync();

        return Results.Ok(new AuthResponse(token, expiresAt, ToProfile(account, account.Profile)));
    }

    static async Task<IResult> Logout(HttpContext http, MobaDbContext db)
    {
        Guid sessionId = http.User.SessionId();
        await db.Sessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync();
        return Results.NoContent();
    }

    static async Task<IResult> GetProfile(HttpContext http, MobaDbContext db)
    {
        Guid accountId = http.User.AccountId();
        Account? account = await db.Accounts.AsNoTracking().Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.Id == accountId);
        if (account?.Profile == null) return Results.NotFound(new ApiError("not_found", "Account not found."));
        return Results.Ok(ToProfile(account, account.Profile));
    }

    // Replaces the stored settings with the JSON object in the body.
    static async Task<IResult> PutSettings(HttpContext http, MobaDbContext db)
    {
        if (http.Request.ContentLength is > MaxSettingsBytes)
            return Results.Json(new ApiError("too_large", "Settings are too large."), statusCode: StatusCodes.Status413PayloadTooLarge);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(http.Request.Body);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new ApiError("invalid_json", "Settings must be valid JSON."));
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Results.BadRequest(new ApiError("invalid_json", "Settings must be a JSON object."));
            string json = document.RootElement.GetRawText();
            if (json.Length > MaxSettingsBytes)
                return Results.Json(new ApiError("too_large", "Settings are too large."), statusCode: StatusCodes.Status413PayloadTooLarge);

            Guid accountId = http.User.AccountId();
            int updated = await db.PlayerProfiles.Where(p => p.AccountId == accountId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Settings, json).SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
            return updated == 0 ? Results.NotFound(new ApiError("not_found", "Account not found.")) : Results.NoContent();
        }
    }

    static string AddSession(MobaDbContext db, Guid accountId, out DateTime expiresAt)
    {
        string token = Tokens.NewToken();
        expiresAt = DateTime.UtcNow + Tokens.SessionLifetime;
        db.Sessions.Add(new Session
        {
            Id = Guid.CreateVersion7(),
            AccountId = accountId,
            TokenHash = Tokens.Hash(token),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        });
        return token;
    }

    static ProfileResponse ToProfile(Account account, PlayerProfile profile)
    {
        using JsonDocument settings = JsonDocument.Parse(string.IsNullOrEmpty(profile.Settings) ? "{}" : profile.Settings);
        return new ProfileResponse(account.Id, account.Email, account.Username, profile.Level, profile.Xp, profile.Mmr,
            settings.RootElement.Clone());
    }

    static IResult EmailTaken() =>
        Results.Conflict(new ApiError("email_taken", "An account with that email already exists."));

    static IResult UsernameTaken() =>
        Results.Conflict(new ApiError("username_taken", "That username is taken."));
}
