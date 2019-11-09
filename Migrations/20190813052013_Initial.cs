using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LucoaBot.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "CustomCommands",
                table => new
                {
                    Id = table.Column<int>()
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>()
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    Command = table.Column<string>(maxLength: 255)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    Response = table.Column<string>(maxLength: 2000)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None)
                },
                constraints: table => { table.PrimaryKey("PK_CustomCommands", x => x.Id); });

            migrationBuilder.CreateTable(
                "GuildConfigs",
                table => new
                {
                    Id = table.Column<int>()
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>()
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    Prefix = table.Column<string>(maxLength: 16)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    LogChannel = table.Column<long>(nullable: true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    StarBoardChannel = table.Column<long>(nullable: true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None)
                },
                constraints: table => { table.PrimaryKey("PK_GuildConfigs", x => x.Id); });

            migrationBuilder.CreateTable(
                "SelfRoles",
                table => new
                {
                    Id = table.Column<int>()
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>()
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    Category = table.Column<string>(maxLength: 255, nullable: true)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None),
                    RoleId = table.Column<long>()
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.None)
                },
                constraints: table => { table.PrimaryKey("PK_SelfRoles", x => x.Id); });

            migrationBuilder.CreateIndex(
                "IX_CustomCommands_GuildId_Command",
                "CustomCommands",
                new[] {"GuildId", "Command"},
                unique: true);

            migrationBuilder.CreateIndex(
                "IX_GuildConfigs_GuildId",
                "GuildConfigs",
                "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                "IX_SelfRoles_GuildId",
                "SelfRoles",
                "GuildId");

            migrationBuilder.CreateIndex(
                "IX_SelfRoles_GuildId_RoleId",
                "SelfRoles",
                new[] {"GuildId", "RoleId"},
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                "CustomCommands");

            migrationBuilder.DropTable(
                "GuildConfigs");

            migrationBuilder.DropTable(
                "SelfRoles");
        }
    }
}