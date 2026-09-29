using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm_App.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserDataOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "RainfallRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Livestock",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Preserve existing records by assigning legacy rows to the first existing account.
            migrationBuilder.Sql("UPDATE Livestock SET OwnerId = (SELECT Id FROM AspNetUsers ORDER BY Id LIMIT 1) WHERE OwnerId = '' AND EXISTS (SELECT 1 FROM AspNetUsers)");
            migrationBuilder.Sql("UPDATE RainfallRecords SET OwnerId = (SELECT Id FROM AspNetUsers ORDER BY Id LIMIT 1) WHERE OwnerId = '' AND EXISTS (SELECT 1 FROM AspNetUsers)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "RainfallRecords");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Livestock");
        }
    }
}
