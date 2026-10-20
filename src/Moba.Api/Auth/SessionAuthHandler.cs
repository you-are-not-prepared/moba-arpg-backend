using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moba.Api.Data;

namespace Moba.Api.Auth;

// "Authorization: Bearer <token>" -> the account of a live session.
public class SessionAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    MobaDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";
    public const string SessionIdClaim = "sid";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        string token = header["Bearer ".Length..].Trim();
        if (token.Length == 0) return AuthenticateResult.Fail("Empty token.");

        string hash = Tokens.Hash(token);
        var session = await db.Sessions.AsNoTracking()
            .Where(s => s.TokenHash == hash && s.ExpiresAt > DateTime.UtcNow)
            .Select(s => new { s.Id, s.AccountId })
            .FirstOrDefaultAsync(Context.RequestAborted);
        if (session == null) return AuthenticateResult.Fail("Unknown or expired session.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, session.AccountId.ToString()),
            new Claim(SessionIdClaim, session.Id.ToString()),
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid AccountId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid SessionId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(SessionAuthHandler.SessionIdClaim)!);
}
