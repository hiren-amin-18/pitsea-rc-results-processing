using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFinishStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Entrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "Entrants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusUpdatedAt",
                table: "Entrants",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Entrants");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "Entrants");

            migrationBuilder.DropColumn(
                name: "StatusUpdatedAt",
                table: "Entrants");
        }
    }
}
