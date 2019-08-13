using LucoaBot.Models;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Services
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // PostgreSQL 10+ is required for this feature.
            modelBuilder.ForNpgsqlUseIdentityColumns();

            modelBuilder
                .Entity<CustomCommand>(
                b =>
                {
                    b.Property(e => e.GuildId).HasConversion<long>();
                    b.HasIndex(e => new { e.GuildId, e.Command }).IsUnique();
                });

            modelBuilder
                .Entity<GuildConfig>(
                b =>
                {
                    b.Property(e => e.GuildId).HasConversion<long>();
                    b.Property(e => e.LogChannel).HasConversion<long>();
                    b.Property(e => e.StarBoardChannel).HasConversion<long>();
                    b.HasIndex(e => e.GuildId).IsUnique();
                });

            modelBuilder
                .Entity<SelfRole>(
                b =>
                {
                    b.Property(e => e.GuildId).HasConversion<long>();
                    b.Property(e => e.RoleId).HasConversion<long>();
                    b.HasIndex(e => e.GuildId);
                    b.HasIndex(e => new { e.GuildId, e.RoleId }).IsUnique();
                });
        }

        public DbSet<CustomCommand> CustomCommands { get; set; }
        public DbSet<GuildConfig> GuildConfigs { get; set; }
        public DbSet<SelfRole> SelfRoles { get; set; }
    }
}
