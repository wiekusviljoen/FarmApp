using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farm_App.Data.Migrations
{
    /// <inheritdoc />
    public partial class PushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FarmPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Auth = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmPushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushServerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PublicKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PrivateKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushServerSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmPushSubscriptions_OwnerId_Endpoint",
                table: "FarmPushSubscriptions",
                columns: new[] { "OwnerId", "Endpoint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmPushSubscriptions");

            migrationBuilder.DropTable(
                name: "PushServerSettings");
        }
    }
}
