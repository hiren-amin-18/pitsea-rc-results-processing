using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    DurationTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    RunnerName = table.Column<string>(type: "TEXT", nullable: false),
                    Club = table.Column<string>(type: "TEXT", nullable: false),
                    EventName = table.Column<string>(type: "TEXT", nullable: false),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceEventId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRecords", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CourseRecords",
                columns: new[] { "Id", "Category", "Club", "CreatedAt", "DurationTicks", "EventDate", "EventName", "EventType", "IsCurrent", "RunnerName", "SourceEventId" },
                values: new object[,]
                {
                    { 1, "Male", "", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 9250000000L, new DateTime(2013, 8, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Crown to Crown", 0, true, "Adam Hickey", null },
                    { 2, "Female", "", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 10810000000L, new DateTime(2015, 12, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Crown to Crown", 0, true, "Jessica Judd", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseRecords_EventType_Category_IsCurrent",
                table: "CourseRecords",
                columns: new[] { "EventType", "Category", "IsCurrent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseRecords");
        }
    }
}
