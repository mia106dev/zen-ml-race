using Microsoft.EntityFrameworkCore;
using ZenMLRace.Core.Entities;

namespace ZenMLRace.Infrastructure.Data;

public class ZenMLDbContext(DbContextOptions<ZenMLDbContext> options) : DbContext(options)
{
    public DbSet<Race> Races { get; set; } = default!;
    public DbSet<Horse> Horses { get; set; } = default!;
    public DbSet<Jockey> Jockeys { get; set; } = default!;
    public DbSet<RaceEntry> RaceEntries { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 複合キーの設定
        modelBuilder.Entity<RaceEntry>()
            .HasKey(re => new { re.RaceId, re.HorseId });

        // リレーション設定
        modelBuilder.Entity<RaceEntry>()
            .HasOne(re => re.Race)
            .WithMany(r => r.RaceEntries)
            .HasForeignKey(re => re.RaceId);

        modelBuilder.Entity<RaceEntry>()
            .HasOne(re => re.Horse)
            .WithMany(h => h.RaceEntries)
            .HasForeignKey(re => re.HorseId);

        modelBuilder.Entity<RaceEntry>()
            .HasOne(re => re.Jockey)
            .WithMany(j => j.RaceEntries)
            .HasForeignKey(re => re.JockeyId);

        // インデックスや制約の設定などもここで行う
    }
}
