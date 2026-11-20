using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dendera.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfileSchools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Schools",
                table: "Profiles",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // The old single rule set mixed sky and physical signals: it became two schools.
            // Any other id maps to "" = all schools.
            migrationBuilder.Sql(
                "UPDATE Profiles SET Schools = 'melothesia,biometeorology' WHERE RuleSetId = 'melothesia-classic';");

            migrationBuilder.DropColumn(
                name: "RuleSetId",
                table: "Profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuleSetId",
                table: "Profiles",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "melothesia-classic");

            migrationBuilder.DropColumn(
                name: "Schools",
                table: "Profiles");
        }
    }
}
