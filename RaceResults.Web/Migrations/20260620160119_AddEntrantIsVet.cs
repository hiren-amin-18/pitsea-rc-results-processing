using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEntrantIsVet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVet",
                table: "Entrants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVet",
                table: "Entrants");
        }
    }
}
