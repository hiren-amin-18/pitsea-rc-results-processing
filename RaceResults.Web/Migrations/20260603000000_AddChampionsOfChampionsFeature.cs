using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddChampionsOfChampionsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChampionOfChampionsScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonYear = table.Column<int>(type: "INTEGER", nullable: false),
                    EntrantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    TotalPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    RaceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChampionOfChampionsScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChampionOfChampionsScores_Entrants_EntrantId",
                        column: x => x.EntrantId,
                        principalTable: "Entrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointsAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeasonYear = table.Column<int>(type: "INTEGER", nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntrantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    PointsAwarded = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    AuditTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointsAuditLogs_Entrants_EntrantId",
                        column: x => x.EntrantId,
                        principalTable: "Entrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PointsAuditLogs_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChampionOfChampionsScores_SeasonYear_EntrantId_Category",
                table: "ChampionOfChampionsScores",
                columns: new[] { "SeasonYear", "EntrantId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsAuditLogs_SeasonYear_EntrantId_EventId",
                table: "PointsAuditLogs",
                columns: new[] { "SeasonYear", "EntrantId", "EventId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChampionOfChampionsScores");

            migrationBuilder.DropTable(
                name: "PointsAuditLogs");
        }
    }
}
