using Microsoft.EntityFrameworkCore;

namespace Moba.Api.Data;

public class MobaDbContext(DbContextOptions<MobaDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<Friendship> Friendships => Set<Friendship>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.Property(a => a.Email).HasMaxLength(254);
            e.Property(a => a.NormalizedEmail).HasMaxLength(254);
            e.Property(a => a.Username).HasMaxLength(24);
            e.Property(a => a.NormalizedUsername).HasMaxLength(24);
            e.HasIndex(a => a.NormalizedEmail).IsUnique();
            e.HasIndex(a => a.NormalizedUsername).IsUnique();
            e.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        });

        b.Entity<Session>(e =>
        {
            e.HasIndex(s => s.TokenHash).IsUnique();
            e.HasIndex(s => s.AccountId);
            e.HasOne(s => s.Account).WithMany(a => a.Sessions).HasForeignKey(s => s.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        });

        b.Entity<PlayerProfile>(e =>
        {
            e.HasKey(p => p.AccountId);
            e.HasOne(p => p.Account).WithOne(a => a.Profile).HasForeignKey<PlayerProfile>(p => p.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Settings).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            e.Property(p => p.Level).HasDefaultValue(1);
            e.Property(p => p.Xp).HasDefaultValue(0);
            e.Property(p => p.Mmr).HasDefaultValue(1000);
            e.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(p => p.Mmr); // leaderboards
        });

        b.Entity<Match>(e =>
        {
            e.Property(m => m.Mode).HasConversion<string>().HasMaxLength(8);
            e.Property(m => m.WinnerTeam).HasConversion<string>().HasMaxLength(8);
            e.Property(m => m.MapLayout).HasMaxLength(32);
            e.Property(m => m.StartedAt).HasDefaultValueSql("now()");
            e.HasIndex(m => m.StartedAt);
            e.HasOne(m => m.CreatedBy).WithMany().HasForeignKey(m => m.CreatedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<MatchPlayer>(e =>
        {
            e.Property(p => p.Team).HasConversion<string>().HasMaxLength(8);
            e.Property(p => p.HeroId).HasMaxLength(32);
            e.HasOne(p => p.Match).WithMany(m => m.Players).HasForeignKey(p => p.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            // Deleting an account keeps the match on record, just without the player link.
            e.HasOne(p => p.Account).WithMany().HasForeignKey(p => p.AccountId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(p => new { p.AccountId, p.MatchId }); // a player's match history
            e.HasIndex(p => new { p.MatchId, p.Slot }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_match_players_bot_has_no_account",
                "NOT is_bot OR account_id IS NULL"));
        });

        b.Entity<Friendship>(e =>
        {
            e.HasKey(f => new { f.AccountId, f.FriendId });
            e.Property(f => f.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(f => f.Account).WithMany().HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Friend).WithMany().HasForeignKey(f => f.FriendId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(f => f.FriendId);
            e.Property(f => f.CreatedAt).HasDefaultValueSql("now()");
            e.ToTable(t => t.HasCheckConstraint("ck_friendships_not_self", "account_id <> friend_id"));
        });
    }
}
