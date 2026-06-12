using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class RevertDefaultEventDateToGoodFriday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reverts FixDefaultEventSeasonDate. The 3 April 2026 date is Good Friday - the real
            // first race of the Crown to Crown series, which runs Good Friday through Boxing Day.
            // The Champions of Champions May-September scoring window is a deliberate subset of
            // the series, so an out-of-window default event is correct: it simply earns no points.
            migrationBuilder.UpdateData(
                table: "Events",
                keyColumn: "Id",
                keyValue: 1,
                column: "EventDate",
                value: new DateTime(2026, 4, 3, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Events",
                keyColumn: "Id",
                keyValue: 1,
                column: "EventDate",
                value: new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
