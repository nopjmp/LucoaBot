using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace LucoaBot.Migrations
{
    public partial class StarboardCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StarboardCache",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StarboardId = table.Column<long>(nullable: false),
                    MessageId = table.Column<long>(nullable: false),
                    GuildId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarboardCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StarboardCache_MessageId_GuildId",
                table: "StarboardCache",
                columns: new[] { "MessageId", "GuildId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StarboardCache");
        }
    }
}
