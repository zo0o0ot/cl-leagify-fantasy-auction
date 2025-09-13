using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using FluentAssertions;

namespace LeagifyFantasyAuction.Tests.Integration;

/// <summary>
/// Integration tests for leave auction business logic focusing on
/// database operations and session token management.
/// </summary>
public class LeaveAuctionBusinessLogicTests : IDisposable
{
    private readonly LeagifyAuctionDbContext _context;

    public LeaveAuctionBusinessLogicTests()
    {
        var options = new DbContextOptionsBuilder<LeagifyAuctionDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new LeagifyAuctionDbContext(options);
    }

    [Fact]
    public async Task LeaveAuction_ValidSessionToken_ShouldMarkUserAsDisconnectedAndClearToken()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var sessionToken = "TESTSESSION123";
        var user = await CreateTestUser(auction.AuctionId, "TestUser", sessionToken, isConnected: true);

        // Act - Simulate leave auction logic
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == sessionToken)
            .FirstOrDefaultAsync();

        userToUpdate.Should().NotBeNull();
        userToUpdate!.IsConnected = false;
        userToUpdate.SessionToken = null;
        userToUpdate.LastActiveDate = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser = await _context.Users.FindAsync(user.UserId);
        updatedUser.Should().NotBeNull();
        updatedUser!.IsConnected.Should().BeFalse();
        updatedUser.SessionToken.Should().BeNull();
        updatedUser.LastActiveDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LeaveAuction_InvalidSessionToken_ShouldNotFindUser()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var user = await CreateTestUser(auction.AuctionId, "TestUser", "VALIDSESSION", isConnected: true);

        // Act - Try to find user with invalid session token
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == "INVALIDSESSION")
            .FirstOrDefaultAsync();

        // Assert
        userToUpdate.Should().BeNull();
        
        // Verify original user is unchanged
        var originalUser = await _context.Users.FindAsync(user.UserId);
        originalUser!.IsConnected.Should().BeTrue();
        originalUser.SessionToken.Should().Be("VALIDSESSION");
    }

    [Fact]
    public async Task LeaveAuction_WrongAuction_ShouldNotFindUser()
    {
        // Arrange
        var auction1 = await CreateTestAuction("TEST123");
        var auction2 = await CreateTestAuction("TEST456");
        var sessionToken = "TESTSESSION123";
        var user = await CreateTestUser(auction1.AuctionId, "TestUser", sessionToken, isConnected: true);

        // Act - Try to leave auction2 with auction1's session token
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction2.AuctionId && u.SessionToken == sessionToken)
            .FirstOrDefaultAsync();

        // Assert
        userToUpdate.Should().BeNull();
        
        // Verify original user in auction1 is unchanged
        var originalUser = await _context.Users.FindAsync(user.UserId);
        originalUser!.IsConnected.Should().BeTrue();
        originalUser.SessionToken.Should().Be(sessionToken);
        originalUser.AuctionId.Should().Be(auction1.AuctionId);
    }

    [Fact]
    public async Task LeaveAuction_MultipleUsersInSameAuction_ShouldOnlyAffectTargetUser()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var sessionToken1 = "SESSION1";
        var sessionToken2 = "SESSION2";
        
        var user1 = await CreateTestUser(auction.AuctionId, "User1", sessionToken1, isConnected: true);
        var user2 = await CreateTestUser(auction.AuctionId, "User2", sessionToken2, isConnected: true);

        // Act - User1 leaves auction
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == sessionToken1)
            .FirstOrDefaultAsync();

        userToUpdate.Should().NotBeNull();
        userToUpdate!.IsConnected = false;
        userToUpdate.SessionToken = null;
        userToUpdate.LastActiveDate = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser1 = await _context.Users.FindAsync(user1.UserId);
        var updatedUser2 = await _context.Users.FindAsync(user2.UserId);
        
        updatedUser1!.IsConnected.Should().BeFalse();
        updatedUser1.SessionToken.Should().BeNull();
        
        updatedUser2!.IsConnected.Should().BeTrue();
        updatedUser2.SessionToken.Should().Be(sessionToken2);
    }

    [Fact]
    public async Task LeaveAuction_UserWithRoles_ShouldLeaveSuccessfullyButKeepRoles()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var team = await CreateTestTeam(auction.AuctionId, "Test Team");
        var sessionToken = "TESTSESSION123";
        var user = await CreateTestUser(auction.AuctionId, "TestUser", sessionToken, isConnected: true);

        // Add user role
        var userRole = new UserRole
        {
            UserId = user.UserId,
            TeamId = team.TeamId,
            Role = "TeamCoach",
            AssignedDate = DateTime.UtcNow
        };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        // Act - User leaves auction
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == sessionToken)
            .FirstOrDefaultAsync();

        userToUpdate.Should().NotBeNull();
        userToUpdate!.IsConnected = false;
        userToUpdate.SessionToken = null;
        userToUpdate.LastActiveDate = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser = await _context.Users.FindAsync(user.UserId);
        updatedUser!.IsConnected.Should().BeFalse();
        updatedUser.SessionToken.Should().BeNull();
        
        // Verify role is still assigned (leaving doesn't remove roles)
        var role = await _context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == user.UserId);
        role.Should().NotBeNull();
        role!.Role.Should().Be("TeamCoach");
        role.TeamId.Should().Be(team.TeamId);
    }

    [Fact]
    public async Task LeaveAuction_SessionTokenClearing_ShouldPreventSubsequentValidation()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var sessionToken = "TESTSESSION123";
        var user = await CreateTestUser(auction.AuctionId, "TestUser", sessionToken, isConnected: true);

        // Act - User leaves (clears session token)
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == sessionToken)
            .FirstOrDefaultAsync();

        userToUpdate.Should().NotBeNull();
        userToUpdate!.IsConnected = false;
        userToUpdate.SessionToken = null;
        userToUpdate.LastActiveDate = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        // Try to validate session after leaving (simulating reconnection attempt)
        var validationUser = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == sessionToken && u.IsConnected)
            .FirstOrDefaultAsync();

        // Assert
        validationUser.Should().BeNull(); // Session should not be valid after leaving
    }

    [Fact]
    public async Task LeaveAuction_AlreadyDisconnectedUser_ShouldStillProcessSuccessfully()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var sessionToken = "TESTSESSION123";
        var user = await CreateTestUser(auction.AuctionId, "TestUser", sessionToken, isConnected: false);

        // Act - User leaves (even though already disconnected)
        var userToUpdate = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId && u.SessionToken == sessionToken)
            .FirstOrDefaultAsync();

        userToUpdate.Should().NotBeNull();
        userToUpdate!.IsConnected = false;
        userToUpdate.SessionToken = null;
        userToUpdate.LastActiveDate = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser = await _context.Users.FindAsync(user.UserId);
        updatedUser!.IsConnected.Should().BeFalse();
        updatedUser.SessionToken.Should().BeNull();
        updatedUser.LastActiveDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetParticipants_WithTeamAssignments_ShouldReturnCorrectTeamData()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var team1 = await CreateTestTeam(auction.AuctionId, "Team Alpha");
        var team2 = await CreateTestTeam(auction.AuctionId, "Team Beta");
        
        var user1 = await CreateTestUser(auction.AuctionId, "Coach1", "SESSION1");
        var user2 = await CreateTestUser(auction.AuctionId, "Coach2", "SESSION2");
        var user3 = await CreateTestUser(auction.AuctionId, "Viewer1", "SESSION3");

        // Assign team roles
        await CreateUserRole(user1.UserId, team1.TeamId, "TeamCoach");
        await CreateUserRole(user2.UserId, team2.TeamId, "TeamCoach");
        await CreateUserRole(user3.UserId, null, "AuctionViewer");

        // Act - Query participants with team information
        var participants = await _context.Users
            .Where(u => u.AuctionId == auction.AuctionId)
            .Select(u => new
            {
                u.UserId,
                u.DisplayName,
                u.IsConnected,
                Roles = _context.UserRoles
                    .Where(ur => ur.UserId == u.UserId)
                    .Select(ur => new
                    {
                        ur.Role,
                        TeamName = ur.Team != null ? ur.Team.TeamName : null
                    })
                    .ToList()
            })
            .ToListAsync();

        // Assert
        participants.Should().HaveCount(3);

        var coach1 = participants.First(p => p.DisplayName == "Coach1");
        coach1.Roles.Should().HaveCount(1);
        coach1.Roles.First().Role.Should().Be("TeamCoach");
        coach1.Roles.First().TeamName.Should().Be("Team Alpha");

        var coach2 = participants.First(p => p.DisplayName == "Coach2");
        coach2.Roles.Should().HaveCount(1);
        coach2.Roles.First().Role.Should().Be("TeamCoach");
        coach2.Roles.First().TeamName.Should().Be("Team Beta");

        var viewer = participants.First(p => p.DisplayName == "Viewer1");
        viewer.Roles.Should().HaveCount(1);
        viewer.Roles.First().Role.Should().Be("AuctionViewer");
        viewer.Roles.First().TeamName.Should().BeNull();
    }

    [Fact]
    public async Task GetParticipants_WithMultipleTeamAssignments_ShouldReturnAllTeams()
    {
        // Arrange
        var auction = await CreateTestAuction();
        var team1 = await CreateTestTeam(auction.AuctionId, "Team Alpha");
        var team2 = await CreateTestTeam(auction.AuctionId, "Team Beta");
        
        var proxyCoach = await CreateTestUser(auction.AuctionId, "ProxyCoach", "PROXYSESSION");

        // Assign multiple teams to proxy coach
        await CreateUserRole(proxyCoach.UserId, team1.TeamId, "ProxyCoach");
        await CreateUserRole(proxyCoach.UserId, team2.TeamId, "ProxyCoach");

        // Act - Query proxy coach's team assignments
        var participant = await _context.Users
            .Where(u => u.UserId == proxyCoach.UserId)
            .Select(u => new
            {
                u.DisplayName,
                Teams = _context.UserRoles
                    .Where(ur => ur.UserId == u.UserId && ur.Team != null)
                    .Select(ur => ur.Team!.TeamName)
                    .ToList()
            })
            .FirstAsync();

        // Assert
        participant.DisplayName.Should().Be("ProxyCoach");
        participant.Teams.Should().HaveCount(2);
        participant.Teams.Should().Contain("Team Alpha");
        participant.Teams.Should().Contain("Team Beta");
    }

    // Helper methods
    private async Task<Auction> CreateTestAuction(string joinCode = "TEST123")
    {
        var auction = new Auction
        {
            Name = "Test Auction",
            JoinCode = joinCode,
            Status = "Draft",
            CreatedDate = DateTime.UtcNow
        };
        _context.Auctions.Add(auction);
        await _context.SaveChangesAsync();
        return auction;
    }

    private async Task<Team> CreateTestTeam(int auctionId, string teamName)
    {
        var team = new Team
        {
            AuctionId = auctionId,
            TeamName = teamName,
            Budget = 200,
            RemainingBudget = 200,
            UserId = 1, // Placeholder user ID
            NominationOrder = 1
        };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task<User> CreateTestUser(int auctionId, string displayName, string sessionToken, bool isConnected = true)
    {
        var user = new User
        {
            AuctionId = auctionId,
            DisplayName = displayName,
            IsConnected = isConnected,
            SessionToken = sessionToken,
            JoinedDate = DateTime.UtcNow,
            LastActiveDate = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<UserRole> CreateUserRole(int userId, int? teamId, string role)
    {
        var userRole = new UserRole
        {
            UserId = userId,
            TeamId = teamId,
            Role = role,
            AssignedDate = DateTime.UtcNow
        };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();
        return userRole;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}