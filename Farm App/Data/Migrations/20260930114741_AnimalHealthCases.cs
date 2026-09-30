using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm_App.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnimalHealthCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimalHealthCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    LivestockId = table.Column<int>(type: "INTEGER", nullable: true),
                    AnimalTag = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Species = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ProblemDescription = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    SuspectedCause = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Recommendation = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    VetAttentionRecommended = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimalHealthCases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimalHealthCases_OwnerId_CreatedAt",
                table: "AnimalHealthCases",
                columns: new[] { "OwnerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimalHealthCases");
        }
    }
}
