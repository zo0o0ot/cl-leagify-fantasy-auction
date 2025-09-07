using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeagifyFantasyAuction.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Schools",
                columns: table => new
                {
                    SchoolId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LogoURL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoFileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schools", x => x.SchoolId);
                });

            migrationBuilder.CreateTable(
                name: "AdminActions",
                columns: table => new
                {
                    AdminActionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: true),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActions", x => x.AdminActionId);
                });

            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    AuctionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JoinCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MasterRecoveryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    StartedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentNominatorUserId = table.Column<int>(type: "int", nullable: true),
                    CurrentSchoolId = table.Column<int>(type: "int", nullable: true),
                    CurrentHighBid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrentHighBidderUserId = table.Column<int>(type: "int", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => x.AuctionId);
                });

            migrationBuilder.CreateTable(
                name: "AuctionSchools",
                columns: table => new
                {
                    AuctionSchoolId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Conference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LeagifyPosition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProjectedPoints = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    NumberOfProspects = table.Column<int>(type: "int", nullable: false),
                    SuggestedAuctionValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ProjectedPointsAboveAverage = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    ProjectedPointsAboveReplacement = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    AveragePointsForPosition = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    ReplacementValueAverageForPosition = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    ImportOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionSchools", x => x.AuctionSchoolId);
                    table.ForeignKey(
                        name: "FK_AuctionSchools_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionSchools_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterPositions",
                columns: table => new
                {
                    RosterPositionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    PositionName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SlotsPerTeam = table.Column<int>(type: "int", nullable: false),
                    ColorCode = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsFlexPosition = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterPositions", x => x.RosterPositionId);
                    table.CheckConstraint("CK_RosterPosition_ColorCode_Format", "[ColorCode] LIKE '#[0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F][0-9A-F]'");
                    table.CheckConstraint("CK_RosterPosition_Slots_Positive", "[SlotsPerTeam] > 0");
                    table.ForeignKey(
                        name: "FK_RosterPositions_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastActiveDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsReconnectionPending = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Users_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BidHistories",
                columns: table => new
                {
                    BidHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    AuctionSchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BidAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    BidType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BidDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsWinningBid = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidHistories", x => x.BidHistoryId);
                    table.CheckConstraint("CK_BidHistory_Amount_Positive", "[BidAmount] > 0");
                    table.ForeignKey(
                        name: "FK_BidHistories_AuctionSchools_AuctionSchoolId",
                        column: x => x.AuctionSchoolId,
                        principalTable: "AuctionSchools",
                        principalColumn: "AuctionSchoolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BidHistories_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BidHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NominationOrders",
                columns: table => new
                {
                    NominationOrderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OrderPosition = table.Column<int>(type: "int", nullable: false),
                    HasNominated = table.Column<bool>(type: "bit", nullable: false),
                    IsSkipped = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NominationOrders", x => x.NominationOrderId);
                    table.ForeignKey(
                        name: "FK_NominationOrders_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NominationOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RemainingBudget = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NominationOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.TeamId);
                    table.CheckConstraint("CK_Team_Budget_Positive", "[Budget] > 0 AND [RemainingBudget] >= 0 AND [RemainingBudget] <= [Budget]");
                    table.ForeignKey(
                        name: "FK_Teams_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Teams_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DraftPicks",
                columns: table => new
                {
                    DraftPickId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    AuctionSchoolId = table.Column<int>(type: "int", nullable: false),
                    RosterPositionId = table.Column<int>(type: "int", nullable: false),
                    WinningBid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NominatedByUserId = table.Column<int>(type: "int", nullable: false),
                    WonByUserId = table.Column<int>(type: "int", nullable: false),
                    PickOrder = table.Column<int>(type: "int", nullable: false),
                    DraftedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsAssignmentConfirmed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftPicks", x => x.DraftPickId);
                    table.CheckConstraint("CK_DraftPick_WinningBid_Positive", "[WinningBid] > 0");
                    table.ForeignKey(
                        name: "FK_DraftPicks_AuctionSchools_AuctionSchoolId",
                        column: x => x.AuctionSchoolId,
                        principalTable: "AuctionSchools",
                        principalColumn: "AuctionSchoolId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "AuctionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftPicks_RosterPositions_RosterPositionId",
                        column: x => x.RosterPositionId,
                        principalTable: "RosterPositions",
                        principalColumn: "RosterPositionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Users_NominatedByUserId",
                        column: x => x.NominatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DraftPicks_Users_WonByUserId",
                        column: x => x.WonByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.UserRoleId);
                    table.ForeignKey(
                        name: "FK_UserRoles_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActions_AdminUserId",
                table: "AdminActions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActions_AuctionId",
                table: "AdminActions",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_CreatedByUserId",
                table: "Auctions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_CurrentHighBidderUserId",
                table: "Auctions",
                column: "CurrentHighBidderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_CurrentNominatorUserId",
                table: "Auctions",
                column: "CurrentNominatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_CurrentSchoolId",
                table: "Auctions",
                column: "CurrentSchoolId");

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

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSchools_AuctionId_IsAvailable",
                table: "AuctionSchools",
                columns: new[] { "AuctionId", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSchools_AuctionId_SchoolId",
                table: "AuctionSchools",
                columns: new[] { "AuctionId", "SchoolId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionSchools_SchoolId",
                table: "AuctionSchools",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_BidHistories_AuctionId",
                table: "BidHistories",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_BidHistories_AuctionSchoolId_BidDate",
                table: "BidHistories",
                columns: new[] { "AuctionSchoolId", "BidDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BidHistories_UserId",
                table: "BidHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_AuctionId",
                table: "DraftPicks",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_AuctionSchoolId",
                table: "DraftPicks",
                column: "AuctionSchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_NominatedByUserId",
                table: "DraftPicks",
                column: "NominatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_RosterPositionId",
                table: "DraftPicks",
                column: "RosterPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_TeamId",
                table: "DraftPicks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_WonByUserId",
                table: "DraftPicks",
                column: "WonByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NominationOrders_AuctionId",
                table: "NominationOrders",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_NominationOrders_UserId",
                table: "NominationOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterPositions_AuctionId",
                table: "RosterPositions",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_Name",
                table: "Schools",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_AuctionId",
                table: "Teams",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_UserId",
                table: "Teams",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TeamId",
                table: "UserRoles",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AuctionId_DisplayName",
                table: "Users",
                columns: new[] { "AuctionId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ConnectionId",
                table: "Users",
                column: "ConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminActions_Auctions_AuctionId",
                table: "AdminActions",
                column: "AuctionId",
                principalTable: "Auctions",
                principalColumn: "AuctionId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AdminActions_Users_AdminUserId",
                table: "AdminActions",
                column: "AdminUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_AuctionSchools_CurrentSchoolId",
                table: "Auctions",
                column: "CurrentSchoolId",
                principalTable: "AuctionSchools",
                principalColumn: "AuctionSchoolId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Users_CreatedByUserId",
                table: "Auctions",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Users_CurrentHighBidderUserId",
                table: "Auctions",
                column: "CurrentHighBidderUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Users_CurrentNominatorUserId",
                table: "Auctions",
                column: "CurrentNominatorUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuctionSchools_Auctions_AuctionId",
                table: "AuctionSchools");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Auctions_AuctionId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AdminActions");

            migrationBuilder.DropTable(
                name: "BidHistories");

            migrationBuilder.DropTable(
                name: "DraftPicks");

            migrationBuilder.DropTable(
                name: "NominationOrders");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "RosterPositions");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Auctions");

            migrationBuilder.DropTable(
                name: "AuctionSchools");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Schools");
        }
    }
}
