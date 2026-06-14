using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Volunteers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    IsClubMember = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFirstAidTrained = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Volunteers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Volunteers_Runners_RunnerId",
                        column: x => x.RunnerId,
                        principalTable: "Runners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOptional = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunAfterCapacity = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresFirstAid = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasEligibilityRestriction = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrePlacedVolunteerId = table.Column<int>(type: "INTEGER", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerRoles_Volunteers_PrePlacedVolunteerId",
                        column: x => x.PrePlacedVolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolunteerRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    WillRunAfter = table.Column<bool>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    PreferredRoleId = table.Column<int>(type: "INTEGER", nullable: true),
                    WantsToRunAfter = table.Column<bool>(type: "INTEGER", nullable: false),
                    WantsNearFinish = table.Column<bool>(type: "INTEGER", nullable: false),
                    CantWalkFar = table.Column<bool>(type: "INTEGER", nullable: false),
                    WantsSeated = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnyRole = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_VolunteerRoles_PreferredRoleId",
                        column: x => x.PreferredRoleId,
                        principalTable: "VolunteerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_VolunteerRoles_VolunteerRoleId",
                        column: x => x.VolunteerRoleId,
                        principalTable: "VolunteerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VolunteerAssignments_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VolunteerRoleEligibilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VolunteerRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolunteerRoleEligibilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolunteerRoleEligibilities_VolunteerRoles_VolunteerRoleId",
                        column: x => x.VolunteerRoleId,
                        principalTable: "VolunteerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VolunteerRoleEligibilities_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "VolunteerRoles",
                columns: new[] { "Id", "Category", "DefaultCount", "EventType", "HasEligibilityRestriction", "IsActive", "IsOptional", "MaxCount", "MinCount", "Name", "PrePlacedVolunteerId", "RequiresFirstAid", "RunAfterCapacity", "SortOrder" },
                values: new object[,]
                {
                    { 1, 0, 1, 0, true, true, false, 1, 1, "Lead", null, false, 0, 1 },
                    { 2, 0, 1, 0, false, true, true, 1, 0, "Shadow Lead", null, false, 0, 2 },
                    { 3, 0, 1, 0, true, true, false, 1, 1, "Results", null, false, 0, 3 },
                    { 4, 1, 2, 0, false, true, false, 2, 2, "Timekeeping", null, false, 0, 4 },
                    { 5, 1, 2, 0, false, true, false, 2, 2, "Course Setup", null, false, 0, 5 },
                    { 6, 1, 2, 0, false, true, false, 2, 1, "Number Collection", null, false, 1, 6 },
                    { 7, 1, 4, 0, false, true, false, 4, 4, "On The Day Registration", null, false, 2, 7 },
                    { 8, 1, 1, 0, false, true, false, 1, 1, "Finish Line Funnel", null, false, 0, 8 },
                    { 9, 1, 2, 0, false, true, false, 2, 2, "Finish Line Results", null, false, 0, 9 },
                    { 10, 1, 1, 0, false, true, false, 1, 1, "First Aid and Prizes", null, true, 0, 10 },
                    { 11, 1, 2, 0, false, true, false, 2, 2, "Tail Runners", null, false, 0, 11 },
                    { 12, 1, 1, 0, false, true, true, 1, 0, "Photographer", null, false, 0, 12 },
                    { 13, 1, 2, 0, false, true, false, 2, 2, "Water Table", null, false, 0, 13 },
                    { 14, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 1", null, false, 0, 14 },
                    { 15, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 2", null, false, 0, 15 },
                    { 16, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 3", null, false, 0, 16 },
                    { 17, 2, 3, 0, false, true, false, 3, 3, "Marshal Point 4", null, false, 0, 17 },
                    { 18, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 5", null, false, 0, 18 },
                    { 19, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 5a", null, false, 0, 19 },
                    { 20, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 6", null, false, 0, 20 },
                    { 21, 2, 2, 0, false, true, false, 2, 2, "Marshal Point 7", null, false, 0, 21 },
                    { 22, 2, 1, 0, false, true, true, 1, 0, "Metal Gate", null, false, 0, 22 },
                    { 23, 2, 1, 0, false, true, false, 1, 1, "First Aid On Course", null, true, 0, 23 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_EventId_VolunteerId",
                table: "VolunteerAssignments",
                columns: new[] { "EventId", "VolunteerId" });

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_PreferredRoleId",
                table: "VolunteerAssignments",
                column: "PreferredRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_VolunteerId",
                table: "VolunteerAssignments",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerAssignments_VolunteerRoleId",
                table: "VolunteerAssignments",
                column: "VolunteerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerRoleEligibilities_VolunteerId",
                table: "VolunteerRoleEligibilities",
                column: "VolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerRoleEligibilities_VolunteerRoleId_VolunteerId",
                table: "VolunteerRoleEligibilities",
                columns: new[] { "VolunteerRoleId", "VolunteerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerRoles_EventType_Name",
                table: "VolunteerRoles",
                columns: new[] { "EventType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VolunteerRoles_PrePlacedVolunteerId",
                table: "VolunteerRoles",
                column: "PrePlacedVolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_RunnerId",
                table: "Volunteers",
                column: "RunnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VolunteerAssignments");

            migrationBuilder.DropTable(
                name: "VolunteerRoleEligibilities");

            migrationBuilder.DropTable(
                name: "VolunteerRoles");

            migrationBuilder.DropTable(
                name: "Volunteers");
        }
    }
}
