using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TimingRows",
                table: "TimingRows");

            migrationBuilder.DropIndex(
                name: "IX_FinishBibRecords_BibNumber",
                table: "FinishBibRecords");

            migrationBuilder.DropIndex(
                name: "IX_FinishBibRecords_Position",
                table: "FinishBibRecords");

            migrationBuilder.DropIndex(
                name: "IX_Entrants_BibNumber",
                table: "Entrants");

            migrationBuilder.AlterColumn<int>(
                name: "Position",
                table: "TimingRows",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "TimingRows",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "TimingRows",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "FinishBibRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "Entrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TimingRows",
                table: "TimingRows",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventName = table.Column<string>(type: "TEXT", nullable: false),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "Id", "EventDate", "EventName", "EventType", "IsCurrent" },
                values: new object[] { 1, new DateTime(2026, 4, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), "Crown to Crown", 0, true });

            migrationBuilder.Sql("DELETE FROM TimingRows;");
            migrationBuilder.Sql("DELETE FROM FinishBibRecords;");
            migrationBuilder.Sql("DELETE FROM Entrants;");

            migrationBuilder.CreateIndex(
                name: "IX_TimingRows_EventId_Position",
                table: "TimingRows",
                columns: new[] { "EventId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinishBibRecords_EventId_BibNumber",
                table: "FinishBibRecords",
                columns: new[] { "EventId", "BibNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinishBibRecords_EventId_Position",
                table: "FinishBibRecords",
                columns: new[] { "EventId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entrants_EventId_BibNumber",
                table: "Entrants",
                columns: new[] { "EventId", "BibNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TimingRows",
                table: "TimingRows");

            migrationBuilder.DropIndex(
                name: "IX_TimingRows_EventId_Position",
                table: "TimingRows");

            migrationBuilder.DropIndex(
                name: "IX_FinishBibRecords_EventId_BibNumber",
                table: "FinishBibRecords");

            migrationBuilder.DropIndex(
                name: "IX_FinishBibRecords_EventId_Position",
                table: "FinishBibRecords");

            migrationBuilder.DropIndex(
                name: "IX_Entrants_EventId_BibNumber",
                table: "Entrants");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "TimingRows");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "TimingRows");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "FinishBibRecords");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Entrants");

            migrationBuilder.AlterColumn<int>(
                name: "Position",
                table: "TimingRows",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TimingRows",
                table: "TimingRows",
                column: "Position");

            migrationBuilder.CreateIndex(
                name: "IX_FinishBibRecords_BibNumber",
                table: "FinishBibRecords",
                column: "BibNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinishBibRecords_Position",
                table: "FinishBibRecords",
                column: "Position",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entrants_BibNumber",
                table: "Entrants",
                column: "BibNumber",
                unique: true);
        }
    }
}
