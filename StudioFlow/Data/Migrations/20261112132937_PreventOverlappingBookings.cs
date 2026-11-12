using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudioFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreventOverlappingBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                """
                ALTER TABLE "Bookings"
                ADD CONSTRAINT booking_no_overlap
                EXCLUDE USING gist (
                    "RoomId" WITH =,
                    tstzrange("StartsAt", "EndsAt", '[)') WITH &&
                )
                WHERE ("Status" = 0);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Bookings" DROP CONSTRAINT booking_no_overlap;
                """
            );
            // btree_gist may be used by other database objects; do not drop it.
        }
    }
}
