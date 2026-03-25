using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Tests.Services;

/// <summary>
/// Unit tests for BiddingService class.
/// Tests bidding completion detection, auto-pass logic, and award functionality.
/// </summary>
public class BiddingServiceTests : IDisposable
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly Mock<ILogger<BiddingService>> _mockLogger;
    private readonly BiddingService _service;
    private readonly DbContextOptions<LeagifyAuctionDbContext> _options;

    public BiddingServiceTests()
    {
        _options = new DbContextOptionsBuilder<LeagifyAuctionDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new LeagifyAuctionDbContext(_options);
        _mockLogger = new Mock<ILogger<BiddingService>>();
        _service = new BiddingService(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CheckBiddingStatusAsync Tests

    [Fact]
    public async Task CheckBiddingStatus_WithNoActiveBidding_ShouldReturnError()
    {
        // Arrange
        var auction = CreateAuction();
        auction.CurrentSchoolId = null; // No active bidding
        await _context.Auctions.AddAsync(auction);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckBiddingStatusAsync(auction.AuctionId);

        // Assert
        result.ErrorMessage.Should().Be("No active bidding");
        result.ShouldEndBidding.Should().BeFalse();
    }

    [Fact]
    public async Task CheckBiddingStatus_WhenAllBiddersPassed_ShouldEndBidding()
    {
        // Arrange
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 3);

        // User 0 is high bidder, Users 1 and 2 pass
        auction.CurrentHighBidderUserId = users[0].UserId;
        auction.CurrentHighBid = 10;
        await _context.SaveChangesAsync();

        // Record passes for users 1 and 2
        _context.BidHistories.AddRange(
            new BidHistory { AuctionId = auction.AuctionId, AuctionSchoolId = school.AuctionSchoolId, UserId = users[1].UserId, BidType = "Pass", BidAmount = 10, BidDate = DateTime.UtcNow },
            new BidHistory { AuctionId = auction.AuctionId, AuctionSchoolId = school.AuctionSchoolId, UserId = users[2].UserId, BidType = "Pass", BidAmount = 10, BidDate = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckBiddingStatusAsync(auction.AuctionId);

        // Assert
        result.ShouldEndBidding.Should().BeTrue();
        result.PassedUserIds.Should().Contain(users[1].UserId);
        result.PassedUserIds.Should().Contain(users[2].UserId);
    }

    [Fact]
    public async Task CheckBiddingStatus_WhenSomeBiddersCanStillBid_ShouldNotEndBidding()
    {
        // Arrange
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 3);

        auction.CurrentHighBidderUserId = users[0].UserId;
        auction.CurrentHighBid = 10;
        await _context.SaveChangesAsync();

        // Only user 1 passes, user 2 can still bid
        _context.BidHistories.Add(
            new BidHistory { AuctionId = auction.AuctionId, AuctionSchoolId = school.AuctionSchoolId, UserId = users[1].UserId, BidType = "Pass", BidAmount = 10, BidDate = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckBiddingStatusAsync(auction.AuctionId);

        // Assert
        result.ShouldEndBidding.Should().BeFalse();
        result.EligibleBidders.Should().Contain(b => b.UserId == users[2].UserId && !b.HasPassed);
    }

    [Fact]
    public async Task CheckBiddingStatus_WhenBidderCantAfford_ShouldAutoPass()
    {
        // Arrange - 2 teams, one with very low budget
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 2);

        // User 0 bids high, user 1's team has very low budget
        auction.CurrentHighBidderUserId = users[0].UserId;
        auction.CurrentHighBid = 150; // High bid
        await _context.SaveChangesAsync();

        // Team 1 has only $10 left but needs to reserve $9 for remaining slots
        teams[1].RemainingBudget = 10;
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckBiddingStatusAsync(auction.AuctionId);

        // Assert
        result.ShouldEndBidding.Should().BeTrue(); // User 1 is auto-passed because max bid ($1) < current high bid ($150)
        result.AutoPassedUserIds.Should().Contain(users[1].UserId);
    }

    [Fact]
    public async Task CheckBiddingStatus_HighBidderIsOnlyEligible_ShouldEndBidding()
    {
        // Arrange - Single team scenario (nominator wins by default)
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 1);

        auction.CurrentHighBidderUserId = users[0].UserId;
        auction.CurrentHighBid = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CheckBiddingStatusAsync(auction.AuctionId);

        // Assert
        result.ShouldEndBidding.Should().BeTrue();
    }

    #endregion

    #region CompleteBiddingAsync Tests

    [Fact]
    public async Task CompleteBidding_WithActiveBidding_ShouldCreateDraftPick()
    {
        // Arrange
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 2);

        auction.CurrentHighBidderUserId = users[0].UserId;
        auction.CurrentHighBid = 25;
        auction.CurrentNominatorUserId = users[0].UserId;
        await _context.SaveChangesAsync();

        var initialBudget = teams[0].RemainingBudget;

        // Act
        var result = await _service.CompleteBiddingAsync(auction.AuctionId);

        // Assert
        result.Success.Should().BeTrue();
        result.DraftPickId.Should().BeGreaterThan(0);
        result.WinningBid.Should().Be(25);
        result.WinnerUserId.Should().Be(users[0].UserId);

        // Verify draft pick was created
        var draftPick = await _context.DraftPicks.FindAsync(result.DraftPickId);
        draftPick.Should().NotBeNull();
        draftPick!.TeamId.Should().Be(teams[0].TeamId);
        draftPick.WinningBid.Should().Be(25);

        // Verify budget was deducted
        var updatedTeam = await _context.Teams.FindAsync(teams[0].TeamId);
        updatedTeam!.RemainingBudget.Should().Be(initialBudget - 25);

        // Verify bidding state was cleared
        var updatedAuction = await _context.Auctions.FindAsync(auction.AuctionId);
        updatedAuction!.CurrentSchoolId.Should().BeNull();
        updatedAuction.CurrentHighBid.Should().BeNull();
        updatedAuction.CurrentHighBidderUserId.Should().BeNull();
    }

    [Fact]
    public async Task CompleteBidding_WithNoActiveBidding_ShouldReturnError()
    {
        // Arrange
        var auction = CreateAuction();
        auction.CurrentSchoolId = null;
        await _context.Auctions.AddAsync(auction);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CompleteBiddingAsync(auction.AuctionId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No active bidding to complete");
    }

    [Fact]
    public async Task CompleteBidding_ShouldAdvanceToNextNominator()
    {
        // Arrange
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 2);

        auction.CurrentHighBidderUserId = users[0].UserId;
        auction.CurrentHighBid = 10;
        auction.CurrentNominatorUserId = users[0].UserId;
        await _context.SaveChangesAsync();

        // Setup nomination order
        _context.NominationOrders.AddRange(
            new NominationOrder { AuctionId = auction.AuctionId, UserId = users[0].UserId, OrderPosition = 1, HasNominated = false },
            new NominationOrder { AuctionId = auction.AuctionId, UserId = users[1].UserId, OrderPosition = 2, HasNominated = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CompleteBiddingAsync(auction.AuctionId);

        // Assert
        result.Success.Should().BeTrue();
        result.NextNominatorUserId.Should().Be(users[1].UserId);
    }

    #endregion

    #region GetMaxBidForUserAsync Tests

    [Fact]
    public async Task GetMaxBid_WithBudgetAndSlots_ShouldCalculateCorrectly()
    {
        // Arrange
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 1);

        // Team has $200 budget, 10 roster slots, 0 picks
        teams[0].RemainingBudget = 200;
        await _context.SaveChangesAsync();

        // Act
        var maxBid = await _service.GetMaxBidForUserAsync(auction.AuctionId, users[0].UserId);

        // Assert
        // MaxBid = Budget - (RemainingSlots - 1) = 200 - (10 - 1) = 191
        maxBid.Should().Be(191);
    }

    [Fact]
    public async Task GetMaxBid_WithFullRoster_ShouldReturnZero()
    {
        // Arrange
        var (auction, school, teams, users) = await SetupActiveBiddingScenario(teamCount: 1);

        // Fill all roster slots with draft picks
        for (int i = 0; i < 10; i++)
        {
            var anotherSchool = new AuctionSchool
            {
                AuctionId = auction.AuctionId,
                SchoolId = 100 + i,
                Conference = "Test",
                LeagifyPosition = "Flex",
                ProjectedPoints = 50
            };
            _context.AuctionSchools.Add(anotherSchool);
            await _context.SaveChangesAsync();

            _context.DraftPicks.Add(new DraftPick
            {
                AuctionId = auction.AuctionId,
                TeamId = teams[0].TeamId,
                AuctionSchoolId = anotherSchool.AuctionSchoolId,
                WinningBid = 10,
                NominatedByUserId = users[0].UserId,
                WonByUserId = users[0].UserId,
                PickOrder = i + 1
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var maxBid = await _service.GetMaxBidForUserAsync(auction.AuctionId, users[0].UserId);

        // Assert
        maxBid.Should().Be(0);
    }

    [Fact]
    public async Task GetMaxBid_WithNoTeam_ShouldReturnZero()
    {
        // Arrange
        var auction = CreateAuction();
        await _context.Auctions.AddAsync(auction);

        var user = new User
        {
            AuctionId = auction.AuctionId,
            DisplayName = "NoTeamUser",
            SessionToken = "token",
            JoinedDate = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var maxBid = await _service.GetMaxBidForUserAsync(auction.AuctionId, user.UserId);

        // Assert
        maxBid.Should().Be(0);
    }

    #endregion

    #region Helper Methods

    private Auction CreateAuction()
    {
        return new Auction
        {
            Name = "Test Auction",
            JoinCode = $"TEST{Guid.NewGuid().ToString()[..4]}",
            MasterRecoveryCode = "MASTER123",
            Status = "InProgress",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };
    }

    private async Task<(Auction auction, AuctionSchool school, List<Team> teams, List<User> users)> SetupActiveBiddingScenario(int teamCount)
    {
        var auction = CreateAuction();
        await _context.Auctions.AddAsync(auction);
        await _context.SaveChangesAsync();

        // Create a school to bid on
        var globalSchool = new School { Name = "Test University" };
        await _context.Schools.AddAsync(globalSchool);
        await _context.SaveChangesAsync();

        var auctionSchool = new AuctionSchool
        {
            AuctionId = auction.AuctionId,
            SchoolId = globalSchool.SchoolId,
            Conference = "Test Conf",
            LeagifyPosition = "Flex",
            ProjectedPoints = 100
        };
        await _context.AuctionSchools.AddAsync(auctionSchool);
        await _context.SaveChangesAsync();

        // Create roster positions (10 flex slots)
        var rosterPosition = new RosterPosition
        {
            AuctionId = auction.AuctionId,
            PositionName = "Flex",
            SlotsPerTeam = 10,
            IsFlexPosition = true,
            DisplayOrder = 1
        };
        await _context.RosterPositions.AddAsync(rosterPosition);
        await _context.SaveChangesAsync();

        var teams = new List<Team>();
        var users = new List<User>();

        for (int i = 0; i < teamCount; i++)
        {
            var user = new User
            {
                AuctionId = auction.AuctionId,
                DisplayName = $"User{i}",
                SessionToken = $"token{i}",
                JoinedDate = DateTime.UtcNow
            };
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var team = new Team
            {
                AuctionId = auction.AuctionId,
                TeamName = $"Team {i}",
                Budget = 200,
                RemainingBudget = 200,
                NominationOrder = i + 1
            };
            await _context.Teams.AddAsync(team);
            await _context.SaveChangesAsync();

            var userRole = new UserRole
            {
                UserId = user.UserId,
                TeamId = team.TeamId,
                Role = "TeamCoach",
                AssignedDate = DateTime.UtcNow
            };
            await _context.UserRoles.AddAsync(userRole);

            teams.Add(team);
            users.Add(user);
        }

        await _context.SaveChangesAsync();

        // Set up active bidding
        auction.CurrentSchoolId = auctionSchool.AuctionSchoolId;
        auction.CurrentNominatorUserId = users[0].UserId;
        await _context.SaveChangesAsync();

        return (auction, auctionSchool, teams, users);
    }

    #endregion
}
