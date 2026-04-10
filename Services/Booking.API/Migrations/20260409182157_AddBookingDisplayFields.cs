using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDisplayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HotelImageUrl",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HotelName",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoomName",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RoomsBooked",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestCount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "HotelImageUrl",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "HotelName",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RoomName",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RoomsBooked",
                table: "Bookings");
        }
    }
}
