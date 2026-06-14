using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicToken",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Events",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsPublished", "PublicToken" },
                values: new object[] { false, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PublicToken",
                table: "Events");
        }
    }
}
