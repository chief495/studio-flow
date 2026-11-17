using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaptureBookingPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedHourlyRate",
                table: "Bookings",
                type: "numeric",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Bookings",
                type: "numeric",
                nullable: true
            );
            migrationBuilder.Sql(
                """
                UPDATE "Bookings" AS b
                SET "AppliedHourlyRate" = t."HourlyRate",
                    "TotalCost" = round(t."HourlyRate" *
                        extract(epoch FROM (b."EndsAt" - b."StartsAt")) / 3600, 2)
                FROM "Rooms" AS r
                JOIN "Tariffs" AS t ON t."Id" = r."TariffId"
                WHERE b."RoomId" = r."Id";
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AppliedHourlyRate", table: "Bookings");

            migrationBuilder.DropColumn(name: "TotalCost", table: "Bookings");
        }
    }
}
