using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBluebellRosterSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WantsRaceHq",
                table: "VolunteerAssignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "VolunteerRoles",
                columns: new[] { "Id", "Category", "DefaultCount", "EventType", "HasEligibilityRestriction", "IsActive", "IsOptional", "MaxCount", "MinCount", "Name", "PrePlacedVolunteerId", "RequiresFirstAid", "RunAfterCapacity", "SortOrder" },
                values: new object[,]
                {
                    { 24, 3, 6, 1, false, true, false, 6, 4, "Number Pick Up", null, false, 3, 24 },
                    { 25, 3, 2, 1, false, true, false, 2, 1, "On The Day Registration", null, false, 1, 25 },
                    { 26, 3, 3, 1, false, true, false, 3, 2, "Refreshments", null, false, 0, 26 },
                    { 27, 3, 1, 1, false, true, true, 1, 0, "Bag Drop", null, false, 0, 27 },
                    { 28, 3, 4, 1, false, true, false, 4, 3, "Car Park Marshal", null, false, 2, 28 },
                    { 29, 0, 1, 1, true, true, false, 1, 1, "Lead", null, false, 0, 29 },
                    { 30, 0, 1, 1, true, true, false, 1, 1, "Results", null, false, 0, 30 },
                    { 31, 1, 2, 1, false, true, false, 2, 2, "Timekeeping", null, false, 0, 31 },
                    { 32, 1, 2, 1, false, true, false, 2, 1, "Finish Line Funnel", null, false, 0, 32 },
                    { 33, 1, 2, 1, false, true, false, 2, 2, "Finish Line Results", null, false, 0, 33 },
                    { 34, 1, 2, 1, false, true, false, 2, 2, "Tail Walker", null, false, 0, 34 },
                    { 35, 1, 4, 1, false, true, false, 4, 4, "Water Table", null, false, 0, 35 },
                    { 36, 1, 1, 1, false, true, true, 1, 0, "Photographer", null, false, 0, 36 },
                    { 37, 1, 1, 1, false, true, false, 1, 1, "Finish Help", null, false, 0, 37 },
                    { 38, 4, 1, 1, false, true, false, 1, 1, "Van Driver", null, false, 0, 38 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "VolunteerRoles",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DropColumn(
                name: "WantsRaceHq",
                table: "VolunteerAssignments");
        }
    }
}
