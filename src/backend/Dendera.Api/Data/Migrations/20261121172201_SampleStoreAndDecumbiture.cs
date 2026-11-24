using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dendera.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SampleStoreAndDecumbiture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "IllnessOnset",
                table: "Journal",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false),
                    Intensity = table.Column<double>(type: "REAL", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_Source_At",
                table: "Samples",
                columns: new[] { "Source", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_Source_Key_At",
                table: "Samples",
                columns: new[] { "Source", "Key", "At" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Samples");

            migrationBuilder.DropColumn(
                name: "IllnessOnset",
                table: "Journal");
        }
    }
}
