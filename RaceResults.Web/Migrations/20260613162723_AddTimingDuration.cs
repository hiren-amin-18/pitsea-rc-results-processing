using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTimingDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DurationTicks",
                table: "TimingRows",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationTicks",
                table: "TimingRows");
        }
    }
}
