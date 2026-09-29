using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm_App.Data.Migrations
{
    /// <inheritdoc />
    public partial class LivestockHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LivestockEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    LivestockId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LivestockEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LivestockEvents_Livestock_LivestockId",
                        column: x => x.LivestockId,
                        principalTable: "Livestock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LivestockEvents_LivestockId",
                table: "LivestockEvents",
                column: "LivestockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LivestockEvents");
        }
    }
}
