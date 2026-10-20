namespace Moba.Api.Data;

// Tables are snake_case in PostgreSQL (accounts, player_profiles, ...) via UseSnakeCaseNamingConvention.
// All timestamps are UTC (timestamptz).

public class Account
{
    public Guid Id { get; set; }
    // As typed by the player, for display.
    public required string Email { get; set; }
    // Lower-cased; unique. Used for lookups so "Bob@x.com" and "bob@x.com" are the same account.
    public required string NormalizedEmail { get; set; }
    public required string Username { get; set; }
    public required string NormalizedUsername { get; set; }
    // A one-way hash (never the password itself). Filled in by the auth code in phase 2.
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public PlayerProfile? Profile { get; set; }
    public List<Session> Sessions { get; set; } = [];
}

// A logged-in device. The client holds the token; only its hash is stored.
public class Session
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class PlayerProfile
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Mmr { get; set; } = 1000;
    // Free-form JSON (keybinds, HUD sizes, saved loadouts...), stored as jsonb.
    public string Settings { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public enum MatchMode { PvE, PvP }

public enum TeamSide { Blue, Red }

public class Match
{
    public Guid Id { get; set; }
    public MatchMode Mode { get; set; }
    // Map layout id from the game ("classic", "zones", "delve").
    public required string MapLayout { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    // Null while the match is running (or if it was abandoned).
    public TeamSide? WinnerTeam { get; set; }

    public List<MatchPlayer> Players { get; set; } = [];
}

// One hero in one match. Bots have no account.
public class MatchPlayer
{
    public long Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }
    public bool IsBot { get; set; }
    public TeamSide Team { get; set; }
    // Hero id from the game ("kael", "torvald", ...).
    public required string HeroId { get; set; }
    public int Level { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int CreepScore { get; set; }
}

public enum FriendshipStatus { Pending, Accepted, Blocked }

// Directed: AccountId sent the request (or did the blocking) to FriendId.
public class Friendship
{
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public Guid FriendId { get; set; }
    public Account Friend { get; set; } = null!;
    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
