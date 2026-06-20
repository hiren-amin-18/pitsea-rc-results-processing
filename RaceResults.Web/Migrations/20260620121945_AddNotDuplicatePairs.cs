using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNotDuplicatePairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotDuplicatePairs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunnerAId = table.Column<int>(type: "INTEGER", nullable: false),
                    RunnerBId = table.Column<int>(type: "INTEGER", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotDuplicatePairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotDuplicatePairs_Runners_RunnerAId",
                        column: x => x.RunnerAId,
                        principalTable: "Runners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotDuplicatePairs_Runners_RunnerBId",
                        column: x => x.RunnerBId,
                        principalTable: "Runners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotDuplicatePairs_RunnerAId_RunnerBId",
                table: "NotDuplicatePairs",
                columns: new[] { "RunnerAId", "RunnerBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotDuplicatePairs_RunnerBId",
                table: "NotDuplicatePairs",
                column: "RunnerBId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotDuplicatePairs");
        }
    }
}
