namespace Moba.Api.Matches;

// JSON bodies (camelCase on the wire). Lists are always wrapped in an object so Unity's JsonUtility can read them.

// Sent when a match starts: the lineup in slot order (slot = index). Exactly one player is the caller (isLocal).
public record StartMatchRequest(string? Mode, string? MapLayout, List<StartMatchPlayer>? Players);

public record StartMatchPlayer(string? Team, string? HeroId, bool IsBot, bool IsLocal);

public record StartMatchResponse(Guid MatchId);

// Sent once when the match ends: every slot's final numbers.
public record MatchResultRequest(string? WinnerTeam, bool CheatsUsed, List<PlayerResult>? Players);

public record PlayerResult(
    int Slot, int Level, int Kills, int Deaths, int Assists, int CreepScore,
    int HeroDamage, int TotalDamage, int DamageTaken, PlayerBuild? Build);

// Final build. Item ids are the game's asset names ("IronBlade", "Spell_Fireball", "Mod_Volley"); "" = empty slot.
public record PlayerBuild(List<string>? Items, List<SpellBuild>? Spells);

// One spell slot (Q, W, E, R in order): the spell ("" = empty), its rank (0 = not learned) and socketed mods.
public record SpellBuild(string? Id, List<string>? Mods, int Rank = 0);

// Everything recorded about one match (GET /me/matches/{id}).
public record MatchDetailResponse(
    Guid MatchId, string Mode, string MapLayout, DateTime StartedAt, int DurationSeconds,
    string WinnerTeam, bool Won, bool CheatsUsed, int Slot, List<MatchDetailPlayer> Players);

// `HasDetails` is false for matches recorded before damage and builds were tracked.
public record MatchDetailPlayer(
    int Slot, string Team, string HeroId, bool IsBot, string? Username,
    int Level, int Kills, int Deaths, int Assists, int CreepScore,
    bool HasDetails, int HeroDamage, int TotalDamage, int DamageTaken, PlayerBuild Build);

public record MatchHistoryResponse(List<MatchSummary> Matches);

// One finished match from the caller's point of view (`slot` is theirs).
public record MatchSummary(
    Guid MatchId, string Mode, string MapLayout, DateTime StartedAt, int DurationSeconds,
    string WinnerTeam, bool Won, bool CheatsUsed, int Slot, List<MatchSummaryPlayer> Players);

public record MatchSummaryPlayer(
    int Slot, string Team, string HeroId, bool IsBot, string? Username,
    int Level, int Kills, int Deaths, int Assists, int CreepScore);

// Totals over finished matches without cheats.
public record PlayerStatsResponse(
    int Matches, int Wins, int Losses, int Kills, int Deaths, int Assists, int CreepScore, List<HeroStats> Heroes);

public record HeroStats(string HeroId, int Matches, int Wins);
