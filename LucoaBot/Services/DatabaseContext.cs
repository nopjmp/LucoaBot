using System.Diagnostics.CodeAnalysis;
using LucoaBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LucoaBot.Services
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class DatabaseContext : DbContext
    {
        private readonly ILoggerFactory _loggerFactory;

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter((category, level) =>
                        (category == DbLoggerCategory.Database.Command.Name ||
                         category == DbLoggerCategory.Infrastructure.Name)
                        && level == LogLevel.Information)
                    .AddSerilog();
            });
        }

        public DbSet<CustomCommand> CustomCommands { get; set; }
        public DbSet<GuildConfig> GuildConfigs { get; set; }
        public DbSet<SelfRole> SelfRoles { get; set; }
        public DbSet<StarboardCache> StarboardCache { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.UseLoggerFactory(_loggerFactory);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Postgres 10+ is required for this feature.
            modelBuilder.UseIdentityColumns();

            modelBuilder
                .Entity<CustomCommand>(
                    b =>
                    {
                        b.Property(e => e.GuildId).HasConversion<long>();
                        b.HasIndex(e => new {e.GuildId, e.Command}).IsUnique();
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
                        b.HasIndex(e => new {e.GuildId, e.RoleId}).IsUnique();
                    });
            
            modelBuilder
                .Entity<StarboardCache>(
                    b =>
                    {
                        b.Property(e => e.StarboardId).HasConversion<long>();
                        b.Property(e => e.MessageId).HasConversion<long>();
                        b.Property(e => e.GuildId).HasConversion<long>();
                        b.HasIndex(e => new {e.MessageId, e.GuildId}).IsUnique();
                    });
        }
    }
}