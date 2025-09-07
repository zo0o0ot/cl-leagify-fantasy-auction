using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagifyFantasyAuction.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueConstraintsForTesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auctions_JoinCode",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_MasterRecoveryCode",
                table: "Auctions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Auctions_JoinCode",
                table: "Auctions",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_MasterRecoveryCode",
                table: "Auctions",
                column: "MasterRecoveryCode",
                unique: true);
        }
    }
}
