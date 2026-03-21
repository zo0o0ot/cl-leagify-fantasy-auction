using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagifyFantasyAuction.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeTeamUserIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPassedOnTestBid",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Teams",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CurrentTestSchoolId",
                table: "Auctions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseManagementAsAdmin",
                table: "Auctions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "AdminActions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "AdminActions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "AdminActions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminActions_ActionDate",
                table: "AdminActions",
                column: "ActionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActions_ActionType",
                table: "AdminActions",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActions_EntityType_EntityId",
                table: "AdminActions",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdminActions_ActionDate",
                table: "AdminActions");

            migrationBuilder.DropIndex(
                name: "IX_AdminActions_ActionType",
                table: "AdminActions");

            migrationBuilder.DropIndex(
                name: "IX_AdminActions_EntityType_EntityId",
                table: "AdminActions");

            migrationBuilder.DropColumn(
                name: "HasPassedOnTestBid",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentTestSchoolId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "UseManagementAsAdmin",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "AdminActions");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "AdminActions");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "AdminActions");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Teams",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
