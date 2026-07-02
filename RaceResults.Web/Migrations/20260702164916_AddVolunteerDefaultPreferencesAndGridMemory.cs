using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddVolunteerDefaultPreferencesAndGridMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultAnyRole",
                table: "Volunteers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCantWalkFar",
                table: "Volunteers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPreferredRoleId",
                table: "Volunteers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultWantsNearFinish",
                table: "Volunteers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultWantsRaceHq",
                table: "Volunteers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultWantsSeated",
                table: "Volunteers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultWantsToRunAfter",
                table: "Volunteers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AllocationCandidateRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    VolunteerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreferredRoleId = table.Column<int>(type: "INTEGER", nullable: true),
                    WantsToRunAfter = table.Column<bool>(type: "INTEGER", nullable: false),
                    WantsNearFinish = table.Column<bool>(type: "INTEGER", nullable: false),
                    CantWalkFar = table.Column<bool>(type: "INTEGER", nullable: false),
                    WantsSeated = table.Column<bool>(type: "INTEGER", nullable: false),
                    WantsRaceHq = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnyRole = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationCandidateRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationCandidateRecords_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllocationCandidateRecords_Volunteers_VolunteerId",
                        column: x => x.VolunteerId,
                        principalTable: "Volunteers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Volunteers_DefaultPreferredRoleId",
                table: "Volunteers",
                column: "DefaultPreferredRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCandidateRecords_EventId_VolunteerId",
                table: "AllocationCandidateRecords",
                columns: new[] { "EventId", "VolunteerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllocationCandidateRecords_VolunteerId",
                table: "AllocationCandidateRecords",
                column: "VolunteerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Volunteers_VolunteerRoles_DefaultPreferredRoleId",
                table: "Volunteers",
                column: "DefaultPreferredRoleId",
                principalTable: "VolunteerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Volunteers_VolunteerRoles_DefaultPreferredRoleId",
                table: "Volunteers");

            migrationBuilder.DropTable(
                name: "AllocationCandidateRecords");

            migrationBuilder.DropIndex(
                name: "IX_Volunteers_DefaultPreferredRoleId",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultAnyRole",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultCantWalkFar",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultPreferredRoleId",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultWantsNearFinish",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultWantsRaceHq",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultWantsSeated",
                table: "Volunteers");

            migrationBuilder.DropColumn(
                name: "DefaultWantsToRunAfter",
                table: "Volunteers");
        }
    }
}
