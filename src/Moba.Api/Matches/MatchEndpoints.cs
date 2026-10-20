using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Moba.Api.Auth;
using Moba.Api.Data;
using Match = Moba.Api.Data.Match;

namespace Moba.Api.Matches;

// POST /matches, POST /matches/{id}/result, GET /me/matches, GET /me/matches/{id}, GET /me/stats
//
// Matches still run inside the game client, so the client reports them. To keep that honest the server:
// creates the match itself (id, start time), takes exactly one result per match and only from the account that
// started it, and rejects impossible numbers. Results that can't be faked at all need a game server (online PvP).
public static partial class MatchEndpoints
{
    public const int MaxPlayers = 10;
    const int MaxHistory = 50;
    // Nobody can destroy a core this fast; a quicker "result" means a broken or tampered client.
    static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(15);

    [GeneratedRegex("^[a-z0-9_]{1,32}$")]
    private static partial Regex IdPattern();

    public static void MapMatchEndpoints(this WebApplication app)
    {
        RouteGroupBuilder matches = app.MapGroup("/matches").RequireAuthorization();
        matches.MapPost("/", Start);
        matches.MapPost("/{id:guid}/result", Report);

        RouteGroupBuilder me = app.MapGroup("/me").RequireAuthorization();
        me.MapGet("/matches", History);
        me.MapGet("/matches/{id:guid}", Detail);
        me.MapGet("/stats", Stats);
    }

    static async Task<IResult> Start(StartMatchRequest request, HttpContext http, MobaDbContext db)
    {
        if (!Enum.TryParse(request.Mode, ignoreCase: true, out MatchMode mode) || !Enum.IsDefined(mode))
            return Invalid("Unknown match mode.");
        if (request.MapLayout == null || !IdPattern().IsMatch(request.MapLayout))
            return Invalid("Unknown map layout.");
        List<StartMatchPlayer>? players = request.Players;
        if (players == null || players.Count == 0 || players.Count > MaxPlayers)
            return Invalid($"A match has 1–{MaxPlayers} players.");
        if (players.Count(p => p.IsLocal) != 1)
            return Invalid("Exactly one player must be you.");
        foreach (StartMatchPlayer p in players)
        {
            if (!Enum.TryParse(p.Team, ignoreCase: true, out TeamSide team) || !Enum.IsDefined(team))
                return Invalid("Unknown team.");
            if (p.HeroId == null || !IdPattern().IsMatch(p.HeroId)) return Invalid("Unknown hero.");
            if (p.IsBot && p.IsLocal) return Invalid("You can't be a bot.");
        }

        Guid accountId = http.User.AccountId();
        var match = new Match
        {
            Id = Guid.CreateVersion7(),
            Mode = mode,
            MapLayout = request.MapLayout,
            StartedAt = DateTime.UtcNow,
            CreatedById = accountId,
        };
        for (int slot = 0; slot < players.Count; slot++)
        {
            StartMatchPlayer p = players[slot];
            match.Players.Add(new MatchPlayer
            {
                Slot = slot,
                AccountId = p.IsLocal ? accountId : null,
                IsBot = p.IsBot,
                Team = Enum.Parse<TeamSide>(p.Team!, ignoreCase: true),
                HeroId = p.HeroId!,
                Level = 1,
            });
        }
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return Results.Created($"/matches/{match.Id}", new StartMatchResponse(match.Id));
    }

    static async Task<IResult> Report(Guid id, MatchResultRequest request, HttpContext http, MobaDbContext db)
    {
        Guid accountId = http.User.AccountId();
        Match? match = await db.Matches.Include(m => m.Players).FirstOrDefaultAsync(m => m.Id == id);
        // Someone else's match looks the same as a missing one.
        if (match == null || match.CreatedById != accountId)
            return Results.NotFound(new ApiError("not_found", "Match not found."));
        if (match.EndedAt != null)
            return Results.Conflict(new ApiError("already_reported", "This match's result was already recorded."));

        if (!Enum.TryParse(request.WinnerTeam, ignoreCase: true, out TeamSide winner) || !Enum.IsDefined(winner))
            return Invalid("Unknown winning team.");
        DateTime now = DateTime.UtcNow;
        if (now - match.StartedAt < MinDuration) return Invalid("That match was too short to be real.");

        List<PlayerResult>? results = request.Players;
        if (results == null || results.Count != match.Players.Count
            || results.Select(r => r.Slot).Distinct().Count() != results.Count
            || results.Any(r => match.Players.All(p => p.Slot != r.Slot)))
            return Invalid("The result must list every player of the match once.");
        foreach (PlayerResult r in results)
        {
            if (r.Level is < 1 or > 30 || r.Kills is < 0 or > 500 || r.Deaths is < 0 or > 500
                || r.Assists is < 0 or > 500 || r.CreepScore is < 0 or > 5000
                || r.HeroDamage is < 0 or > MaxDamage || r.TotalDamage is < 0 or > MaxDamage
                || r.DamageTaken is < 0 or > MaxDamage || r.HeroDamage > r.TotalDamage)
                return Invalid("Some of the numbers are impossible.");
            if (!IsValidBuild(r.Build)) return Invalid("A player's build isn't valid.");
        }

        foreach (PlayerResult r in results)
        {
            MatchPlayer player = match.Players.First(p => p.Slot == r.Slot);
            player.Level = r.Level;
            player.Kills = r.Kills;
            player.Deaths = r.Deaths;
            player.Assists = r.Assists;
            player.CreepScore = r.CreepScore;
            player.HeroDamage = r.HeroDamage;
            player.TotalDamage = r.TotalDamage;
            player.DamageTaken = r.DamageTaken;
            player.Build = r.Build != null ? JsonSerializer.Serialize(Normalize(r.Build), JsonOptions) : null;
        }
        match.WinnerTeam = winner;
        match.EndedAt = now;
        match.CheatsUsed = request.CheatsUsed;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // The caller's finished matches, newest first.
    static async Task<IResult> History(HttpContext http, MobaDbContext db, int? limit)
    {
        Guid accountId = http.User.AccountId();
        int take = Math.Clamp(limit ?? 20, 1, MaxHistory);

        List<Match> matches = await db.Matches.AsNoTracking()
            .Where(m => m.EndedAt != null && m.Players.Any(p => p.AccountId == accountId))
            .OrderByDescending(m => m.StartedAt)
            .Take(take)
            .Include(m => m.Players).ThenInclude(p => p.Account)
            .AsSplitQuery()
            .ToListAsync();

        List<MatchSummary> summaries = matches.Select(m =>
        {
            MatchPlayer mine = m.Players.First(p => p.AccountId == accountId);
            return new MatchSummary(
                m.Id, m.Mode.ToString(), m.MapLayout, m.StartedAt,
                (int)(m.EndedAt!.Value - m.StartedAt).TotalSeconds,
                m.WinnerTeam?.ToString() ?? "", mine.Team == m.WinnerTeam, m.CheatsUsed, mine.Slot,
                m.Players.OrderBy(p => p.Slot).Select(p => new MatchSummaryPlayer(
                    p.Slot, p.Team.ToString(), p.HeroId, p.IsBot, p.Account?.Username,
                    p.Level, p.Kills, p.Deaths, p.Assists, p.CreepScore)).ToList());
        }).ToList();

        return Results.Ok(new MatchHistoryResponse(summaries));
    }

    static async Task<IResult> Stats(HttpContext http, MobaDbContext db)
    {
        Guid accountId = http.User.AccountId();
        var rows = await db.MatchPlayers.AsNoTracking()
            .Where(p => p.AccountId == accountId && p.Match.EndedAt != null && !p.Match.CheatsUsed)
            .Select(p => new { p.HeroId, Won = p.Team == p.Match.WinnerTeam, p.Kills, p.Deaths, p.Assists, p.CreepScore })
            .ToListAsync();

        int wins = rows.Count(r => r.Won);
        List<HeroStats> heroes = rows.GroupBy(r => r.HeroId)
            .Select(g => new HeroStats(g.Key, g.Count(), g.Count(r => r.Won)))
            .OrderByDescending(h => h.Matches).ThenBy(h => h.HeroId)
            .ToList();
        return Results.Ok(new PlayerStatsResponse(rows.Count, wins, rows.Count - wins,
            rows.Sum(r => r.Kills), rows.Sum(r => r.Deaths), rows.Sum(r => r.Assists), rows.Sum(r => r.CreepScore), heroes));
    }

    // Everything about one of the caller's finished matches, including damage and builds.
    static async Task<IResult> Detail(Guid id, HttpContext http, MobaDbContext db)
    {
        Guid accountId = http.User.AccountId();
        Match? match = await db.Matches.AsNoTracking()
            .Where(m => m.Id == id && m.EndedAt != null && m.Players.Any(p => p.AccountId == accountId))
            .Include(m => m.Players).ThenInclude(p => p.Account)
            .AsSplitQuery()
            .FirstOrDefaultAsync();
        if (match == null) return Results.NotFound(new ApiError("not_found", "Match not found."));

        MatchPlayer mine = match.Players.First(p => p.AccountId == accountId);
        List<MatchDetailPlayer> players = match.Players.OrderBy(p => p.Slot).Select(p => new MatchDetailPlayer(
            p.Slot, p.Team.ToString(), p.HeroId, p.IsBot, p.Account?.Username,
            p.Level, p.Kills, p.Deaths, p.Assists, p.CreepScore,
            p.Build != null, p.HeroDamage, p.TotalDamage, p.DamageTaken,
            p.Build != null ? JsonSerializer.Deserialize<PlayerBuild>(p.Build, JsonOptions)! : EmptyBuild)).ToList();

        return Results.Ok(new MatchDetailResponse(
            match.Id, match.Mode.ToString(), match.MapLayout, match.StartedAt,
            (int)(match.EndedAt!.Value - match.StartedAt).TotalSeconds,
            match.WinnerTeam?.ToString() ?? "", mine.Team == match.WinnerTeam, match.CheatsUsed, mine.Slot, players));
    }

    // ---------- Builds ----------

    const int MaxDamage = 10_000_000;
    const int MaxItems = 8;
    const int MaxSpells = 4;
    const int MaxMods = 4;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    static readonly PlayerBuild EmptyBuild = new([], []);

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex ItemIdPattern();

    static bool IsItemId(string? id) => string.IsNullOrEmpty(id) || ItemIdPattern().IsMatch(id);

    static bool IsValidBuild(PlayerBuild? build)
    {
        if (build == null) return true;
        if (build.Items is { Count: > MaxItems } || build.Spells is { Count: > MaxSpells }) return false;
        if (build.Items != null && !build.Items.All(IsItemId)) return false;
        return build.Spells == null || build.Spells.All(s =>
            s != null && IsItemId(s.Id) && (s.Mods == null || (s.Mods.Count <= MaxMods && s.Mods.All(IsItemId))));
    }

    // Nulls become empty, so stored builds always have the same shape.
    static PlayerBuild Normalize(PlayerBuild build) => new(
        build.Items?.Select(i => i ?? "").ToList() ?? [],
        build.Spells?.Select(s => new SpellBuild(s.Id ?? "", s.Mods?.Select(m => m ?? "").ToList() ?? [])).ToList() ?? []);

    static IResult Invalid(string message) => Results.BadRequest(new ApiError("invalid_input", message));
}
