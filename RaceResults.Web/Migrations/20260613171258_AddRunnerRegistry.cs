using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRunnerRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RunnerId",
                table: "Entrants",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Runners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Club = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    Age = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalReference = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entrants_RunnerId",
                table: "Entrants",
                column: "RunnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrants_Runners_RunnerId",
                table: "Entrants",
                column: "RunnerId",
                principalTable: "Runners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Entrants_Runners_RunnerId",
                table: "Entrants");

            migrationBuilder.DropTable(
                name: "Runners");

            migrationBuilder.DropIndex(
                name: "IX_Entrants_RunnerId",
                table: "Entrants");

            migrationBuilder.DropColumn(
                name: "RunnerId",
                table: "Entrants");
        }
    }
}
