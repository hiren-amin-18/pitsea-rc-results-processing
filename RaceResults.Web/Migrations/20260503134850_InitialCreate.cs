using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Entrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BibNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Club = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinishBibRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    BibNumber = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinishBibRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimingRows",
                columns: table => new
                {
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Time = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimingRows", x => x.Position);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entrants_BibNumber",
                table: "Entrants",
                column: "BibNumber",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Entrants");

            migrationBuilder.DropTable(
                name: "FinishBibRecords");

            migrationBuilder.DropTable(
                name: "TimingRows");
        }
    }
}
