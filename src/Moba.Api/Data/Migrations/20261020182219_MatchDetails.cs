using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moba.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MatchDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "build",
                table: "match_players",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "damage_taken",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "hero_damage",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_damage",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "build",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "damage_taken",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "hero_damage",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "total_damage",
                table: "match_players");
        }
    }
}
