using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagifyFantasyAuction.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProxyAliasToUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProxyAlias",
                table: "UserRoles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProxyAlias",
                table: "UserRoles");
        }
    }
}
