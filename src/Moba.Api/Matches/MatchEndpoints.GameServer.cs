using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Moba.Api.Auth;
using Moba.Api.Data;
using Match = Moba.Api.Data.Match;

namespace Moba.Api.Matches;

// Matches played on a game server (online PvP), which runs the match itself and so is the one to record it.
//   POST /me/join-ticket                  player: a ticket to hand to the game server when joining
//   POST /server/join-tickets/redeem      game server: whose ticket is this?
//   POST /server/matches                  game server: a finished match, every player's numbers and build
// Game-server calls carry the shared key (config "GameServer:Key", env GameServer__Key) in X-Game-Server-Key.
// Without a key configured they're switched off.
public static partial class MatchEndpoints
{
    public const string ServerKeyHeader = "X-Game-Server-Key";

    public static void MapGameServerEndpoints(this WebApplication app)
    {
        app.MapPost("/me/join-ticket", CreateJoinTicket).RequireAuthorization();

        RouteGroupBuilder server = app.MapGroup("/server").AddEndpointFilter(RequireServerKey);
        server.MapPost("/join-tickets/redeem", RedeemJoinTicket);
        server.MapPost("/matches", RecordServerMatch);
    }

    static IResult CreateJoinTicket(HttpContext http)
    {
        DateTime expiresAt = DateTime.UtcNow + JoinTickets.Lifetime;
        return Results.Ok(new JoinTicketResponse(JoinTickets.Create(http.User.AccountId(), expiresAt), expiresAt));
    }

    static async ValueTask<object?> RequireServerKey(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;
        string? key = http.RequestServices.GetRequiredService<IConfiguration>()["GameServer:Key"];
        if (string.IsNullOrEmpty(key))
            return Results.Json(new ApiError("not_configured", "Game server recording isn't set up on this backend."),
                statusCode: StatusCodes.Status503ServiceUnavailable);

        string given = http.Request.Headers[ServerKeyHeader].ToString();
        bool ok = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return ok
            ? await next(context)
            : Results.Json(new ApiError("unauthorized", "Unknown game server."), statusCode: StatusCodes.Status401Unauthorized);
    }

    static async Task<IResult> RedeemJoinTicket(RedeemTicketRequest request, MobaDbContext db)
    {
        if (!JoinTickets.TryRead(request.Ticket, out Guid accountId))
            return Invalid("That join ticket isn't valid (or has expired).");
        string? username = await db.Accounts.Where(a => a.Id == accountId).Select(a => a.Username).FirstOrDefaultAsync();
        return username == null
            ? Results.NotFound(new ApiError("not_found", "That account no longer exists."))
            : Results.Ok(new RedeemTicketResponse(accountId, username));
    }

    // The whole match at once, as the game server saw it. Players without an account (bots, players who weren't
    // logged in) are kept for the lineup.
    static async Task<IResult> RecordServerMatch(ServerMatchRequest request, MobaDbContext db)
    {
        if (!Enum.TryParse(request.Mode, ignoreCase: true, out MatchMode mode) || !Enum.IsDefined(mode))
            return Invalid("Unknown match mode.");
        if (request.MapLayout == null || !IdPattern().IsMatch(request.MapLayout))
            return Invalid("Unknown map layout.");
        if (!Enum.TryParse(request.WinnerTeam, ignoreCase: true, out TeamSide winner) || !Enum.IsDefined(winner))
            return Invalid("Unknown winning team.");
        if (request.DurationSeconds < MinDuration.TotalSeconds || request.DurationSeconds > 24 * 3600)
            return Invalid("That match's length isn't possible.");
        List<ServerMatchPlayer>? players = request.Players;
        if (players == null || players.Count == 0 || players.Count > MaxPlayers)
            return Invalid($"A match has 1–{MaxPlayers} players.");

        var accounts = new List<Guid?>();
        foreach (ServerMatchPlayer p in players)
        {
            if (!Enum.TryParse(p.Team, ignoreCase: true, out TeamSide _)) return Invalid("Unknown team.");
            if (p.HeroId == null || !IdPattern().IsMatch(p.HeroId)) return Invalid("Unknown hero.");
            Guid? account = null;
            if (!string.IsNullOrEmpty(p.AccountId))
            {
                if (p.IsBot || !Guid.TryParse(p.AccountId, out Guid id)) return Invalid("A player's account isn't valid.");
                account = id;
            }
            accounts.Add(account);
            if (!IsPossible(p.Result)) return Invalid("Some of the numbers are impossible.");
            if (!IsValidBuild(p.Result.Build)) return Invalid("A player's build isn't valid.");
        }
        List<Guid> known = accounts.OfType<Guid>().ToList();
        if (known.Count != known.Distinct().Count()) return Invalid("An account can only play one hero in a match.");
        if (await db.Accounts.CountAsync(a => known.Contains(a.Id)) != known.Count) return Invalid("Unknown account.");

        DateTime now = DateTime.UtcNow;
        var match = new Match
        {
            Id = Guid.CreateVersion7(),
            Mode = mode,
            MapLayout = request.MapLayout,
            StartedAt = now - TimeSpan.FromSeconds(request.DurationSeconds),
            EndedAt = now,
            WinnerTeam = winner,
            CheatsUsed = request.CheatsUsed,
        };
        for (int slot = 0; slot < players.Count; slot++)
        {
            ServerMatchPlayer p = players[slot];
            var player = new MatchPlayer
            {
                Slot = slot,
                AccountId = accounts[slot],
                IsBot = p.IsBot,
                Team = Enum.Parse<TeamSide>(p.Team!, ignoreCase: true),
                HeroId = p.HeroId!,
            };
            ApplyResult(player, p.Result);
            match.Players.Add(player);
        }
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return Results.Created($"/matches/{match.Id}", new StartMatchResponse(match.Id));
    }
}
