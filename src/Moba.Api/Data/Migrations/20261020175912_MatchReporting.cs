using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Moba.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MatchReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_match_players_match_id",
                table: "match_players");

            migrationBuilder.AddColumn<bool>(
                name: "cheats_used",
                table: "matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_id",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "assists",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "slot",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_matches_created_by_id",
                table: "matches",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_match_id_slot",
                table: "match_players",
                columns: new[] { "match_id", "slot" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_accounts_created_by_id",
                table: "matches",
                column: "created_by_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_matches_accounts_created_by_id",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_created_by_id",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_match_players_match_id_slot",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "cheats_used",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "created_by_id",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "assists",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "slot",
                table: "match_players");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_match_id",
                table: "match_players",
                column: "match_id");
        }
    }
}
