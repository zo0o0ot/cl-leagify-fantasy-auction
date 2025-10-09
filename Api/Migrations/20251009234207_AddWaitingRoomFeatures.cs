using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagifyFantasyAuction.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitingRoomFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasTestedBidding",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReadyToDraft",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestSchool",
                table: "AuctionSchools",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasTestedBidding",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsReadyToDraft",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsTestSchool",
                table: "AuctionSchools");
        }
    }
}
