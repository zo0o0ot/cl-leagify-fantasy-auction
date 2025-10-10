using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagifyFantasyAuction.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeAuctionSchoolIdNullableForTestBids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AuctionSchoolId",
                table: "BidHistories",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AuctionSchoolId",
                table: "BidHistories",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
