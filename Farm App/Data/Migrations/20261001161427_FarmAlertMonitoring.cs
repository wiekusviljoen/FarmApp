using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm_App.Data.Migrations
{
    /// <inheritdoc />
    public partial class FarmAlertMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertStateJson",
                table: "PushServerSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastThunderAlertKey",
                table: "FarmPushSubscriptions",
                type: "TEXT",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "FarmPushSubscriptions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "FarmPushSubscriptions",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertStateJson",
                table: "PushServerSettings");

            migrationBuilder.DropColumn(
                name: "LastThunderAlertKey",
                table: "FarmPushSubscriptions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "FarmPushSubscriptions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "FarmPushSubscriptions");
        }
    }
}
