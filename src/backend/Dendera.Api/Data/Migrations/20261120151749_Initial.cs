using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dendera.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BirthLocal = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BirthUtcOffsetMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    BirthTimeKnown = table.Column<bool>(type: "INTEGER", nullable: false),
                    BirthPlace = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    BirthLatitude = table.Column<double>(type: "REAL", nullable: false),
                    BirthLongitude = table.Column<double>(type: "REAL", nullable: false),
                    BirthTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentPlace = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CurrentLatitude = table.Column<double>(type: "REAL", nullable: false),
                    CurrentLongitude = table.Column<double>(type: "REAL", nullable: false),
                    CurrentTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    HouseSystem = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RuleSetId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Journal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Energy = table.Column<int>(type: "INTEGER", nullable: true),
                    Mood = table.Column<int>(type: "INTEGER", nullable: true),
                    Sleep = table.Column<int>(type: "INTEGER", nullable: true),
                    Focus = table.Column<int>(type: "INTEGER", nullable: true),
                    Stress = table.Column<int>(type: "INTEGER", nullable: true),
                    Gut = table.Column<int>(type: "INTEGER", nullable: true),
                    Head = table.Column<int>(type: "INTEGER", nullable: true),
                    Body = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journal_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Journal_ProfileId_Date",
                table: "Journal",
                columns: new[] { "ProfileId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Journal");

            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
