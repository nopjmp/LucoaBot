﻿// <auto-generated />
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LucoaBot.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    [Migration("20190813052013_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "3.0.0-preview7.19362.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("LucoaBot.Data.CustomCommand", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("Command")
                        .IsRequired()
                        .HasMaxLength(255);

                    b.Property<long>("GuildId");

                    b.Property<string>("Response")
                        .IsRequired()
                        .HasMaxLength(2000);

                    b.HasKey("Id");

                    b.HasIndex("GuildId", "Command")
                        .IsUnique();

                    b.ToTable("CustomCommands");
                });

            modelBuilder.Entity("LucoaBot.Data.GuildConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<long>("GuildId");

                    b.Property<long?>("LogChannel");

                    b.Property<string>("Prefix")
                        .IsRequired()
                        .HasMaxLength(16);

                    b.Property<long?>("StarBoardChannel");

                    b.HasKey("Id");

                    b.HasIndex("GuildId")
                        .IsUnique();

                    b.ToTable("GuildConfigs");
                });

            modelBuilder.Entity("LucoaBot.Data.SelfRole", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("Category")
                        .HasMaxLength(255);

                    b.Property<long>("GuildId");

                    b.Property<long>("RoleId");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.HasIndex("GuildId", "RoleId")
                        .IsUnique();

                    b.ToTable("SelfRoles");
                });
#pragma warning restore 612, 618
        }
    }
}
