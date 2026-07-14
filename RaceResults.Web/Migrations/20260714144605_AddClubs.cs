using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaceResults.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clubs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Clubs",
                columns: new[] { "Id", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, true, "Aberystwyth AC" },
                    { 2, true, "Bad Boy Running" },
                    { 3, true, "Barking Road Runners" },
                    { 4, true, "Barking Running Club" },
                    { 5, true, "Basildon Athletics Club" },
                    { 6, true, "Basildon CC" },
                    { 7, true, "Benfleet Running Club" },
                    { 8, true, "Billericay Striders" },
                    { 9, true, "Braintree & District Athletic Club" },
                    { 10, true, "Brentwood Beagles Athletics Club" },
                    { 11, true, "Brentwood Running Club" },
                    { 12, true, "Castle Point Joggers" },
                    { 13, true, "Castle Point Young Runners" },
                    { 14, true, "Chelmsford Athletics" },
                    { 15, true, "City of Southend On Sea AC" },
                    { 16, true, "Corringham Running Club" },
                    { 17, true, "Dagenham 88 Runners" },
                    { 18, true, "Daws Heath Harriers" },
                    { 19, true, "Dengie 100 Runners" },
                    { 20, true, "East Essex Triathlon Club" },
                    { 21, true, "East London Runners" },
                    { 22, true, "Fordy Runs Running Club" },
                    { 23, true, "Harold Wood Running Club" },
                    { 24, true, "Havering '90 Joggers" },
                    { 25, true, "Havering AC" },
                    { 26, true, "Havering Tri" },
                    { 27, true, "Hockley Trail Runners" },
                    { 28, true, "Hot Steppers" },
                    { 29, true, "Ilford AC" },
                    { 30, true, "JBR Run and Tri Club" },
                    { 31, true, "Kingswood Running Club" },
                    { 32, true, "Leigh-on-Sea Striders" },
                    { 33, true, "London Heathside" },
                    { 34, true, "Lonely Goat RC" },
                    { 35, true, "Maldon Soul Runners" },
                    { 36, true, "Mid Essex Casuals" },
                    { 37, true, "Nuclear Races Striders" },
                    { 38, true, "Pewsey Vale Running Club" },
                    { 39, true, "Phoenix Striders" },
                    { 40, true, "Pitsea Running Club" },
                    { 41, true, "Rayleigh Rat Runners" },
                    { 42, true, "RED Runners" },
                    { 43, true, "Rochford Running Club" },
                    { 44, true, "South Woodham Runners" },
                    { 45, true, "Springfield Striders RC" },
                    { 46, true, "SS Athletics" },
                    { 47, true, "St Edmund Pacers" },
                    { 48, true, "Thames Hare & Hounds" },
                    { 49, true, "Thurrock Harriers" },
                    { 50, true, "Thurrock Nomads" },
                    { 51, true, "Trail Running Association" },
                    { 52, true, "Vegan Runners UK" },
                    { 53, true, "Ware Joggers" },
                    { 54, true, "Witham Running Club" },
                    { 55, true, "Woman of Wickford" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_Name",
                table: "Clubs",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clubs");
        }
    }
}
