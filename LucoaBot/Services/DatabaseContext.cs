﻿using System.Diagnostics.CodeAnalysis;
using LucoaBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LucoaBot.Services
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class DatabaseContext : DbContext
    {
        private readonly ILoggerFactory _loggerFactory;

        public DatabaseContext(DbContextOptions options, ILoggerFactory loggerFactory) : base(options)
        {
            _loggerFactory = loggerFactory;
        }

        public DbSet<CustomCommand> CustomCommands { get; set; }
        public DbSet<GuildConfig> GuildConfigs { get; set; }
        public DbSet<SelfRole> SelfRoles { get; set; }

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
        }
    }
}