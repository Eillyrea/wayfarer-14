using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class WayfarerRoundSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wayfarer_round_summaries",
                columns: table => new
                {
                    round_number = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    round_start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    round_end_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    profit_loss_data = table.Column<byte[]>(type: "jsonb", nullable: true),
                    player_stories = table.Column<byte[]>(type: "jsonb", nullable: true),
                    player_manifest = table.Column<byte[]>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_round_summaries", x => x.round_number);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wayfarer_round_summaries");
        }
    }
}
