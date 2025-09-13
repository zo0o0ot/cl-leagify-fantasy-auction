using Xunit;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;

namespace LeagifyFantasyAuction.Tests.Integration
{
    /// <summary>
    /// Integration tests for session token functionality and database operations.
    /// Tests the core business logic for session management and user connection tracking.
    /// </summary>
    public class SessionTokenIntegrationTests : IDisposable
    {
        private readonly LeagifyAuctionDbContext _dbContext;

        public SessionTokenIntegrationTests()
        {
            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<LeagifyAuctionDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new LeagifyAuctionDbContext(options);
        }

        [Fact]
        public async Task UserSessionToken_CanBeStoredAndRetrieved()
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var sessionToken = Guid.NewGuid().ToString();
            var user = new User
            {
                AuctionId = 1,
                DisplayName = "Test User",
                SessionToken = sessionToken,
                IsConnected = true
            };

            // Act
            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Assert
            var retrievedUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken);
            
            retrievedUser.Should().NotBeNull();
            retrievedUser!.SessionToken.Should().Be(sessionToken);
            retrievedUser.DisplayName.Should().Be("Test User");
            retrievedUser.IsConnected.Should().BeTrue();
        }

        [Fact]
        public async Task UserConnectionStatus_CanBeUpdatedBasedOnConnectionId()
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var user = new User
            {
                AuctionId = 1,
                DisplayName = "Test User",
                IsConnected = false,
                ConnectionId = null
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var connectionId = "connection-123";

            // Act - Simulate connection
            user.IsConnected = true;
            user.ConnectionId = connectionId;
            user.LastActiveDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Assert connection state
            var connectedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId);
            connectedUser.Should().NotBeNull();
            connectedUser!.IsConnected.Should().BeTrue();
            connectedUser.ConnectionId.Should().Be(connectionId);

            // Act - Simulate disconnection
            var userToDisconnect = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.ConnectionId == connectionId);
            
            userToDisconnect!.IsConnected = false;
            userToDisconnect.ConnectionId = null;
            await _dbContext.SaveChangesAsync();

            // Assert disconnection state
            var disconnectedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId);
            disconnectedUser!.IsConnected.Should().BeFalse();
            disconnectedUser.ConnectionId.Should().BeNull();
        }

        [Fact]
        public async Task GetAuctionParticipants_ReturnsCorrectConnectionStatuses()
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var users = new[]
            {
                new User
                {
                    AuctionId = 1,
                    DisplayName = "Connected User",
                    IsConnected = true,
                    ConnectionId = "conn-1",
                    LastActiveDate = DateTime.UtcNow.AddMinutes(-2)
                },
                new User
                {
                    AuctionId = 1,
                    DisplayName = "Disconnected User",
                    IsConnected = false,
                    ConnectionId = null,
                    LastActiveDate = DateTime.UtcNow.AddMinutes(-10)
                }
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.AddRange(users);
            await _dbContext.SaveChangesAsync();

            // Act
            var participants = await _dbContext.Users
                .Where(u => u.AuctionId == auction.AuctionId)
                .Select(u => new
                {
                    u.DisplayName,
                    u.IsConnected,
                    u.ConnectionId,
                    u.LastActiveDate
                })
                .ToListAsync();

            // Assert
            participants.Should().HaveCount(2);
            
            var connectedParticipant = participants.First(p => p.DisplayName == "Connected User");
            connectedParticipant.IsConnected.Should().BeTrue();
            connectedParticipant.ConnectionId.Should().Be("conn-1");

            var disconnectedParticipant = participants.First(p => p.DisplayName == "Disconnected User");
            disconnectedParticipant.IsConnected.Should().BeFalse();
            disconnectedParticipant.ConnectionId.Should().BeNull();
        }

        [Fact]
        public async Task SessionTokenValidation_WorksCorrectly()
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var validToken = "valid-token-123";
            var user = new User
            {
                AuctionId = 1,
                DisplayName = "Test User",
                SessionToken = validToken
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Act & Assert - Valid token
            var userWithValidToken = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.AuctionId == 1 && u.SessionToken == validToken);
            userWithValidToken.Should().NotBeNull();

            // Act & Assert - Invalid token
            var userWithInvalidToken = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.AuctionId == 1 && u.SessionToken == "invalid-token");
            userWithInvalidToken.Should().BeNull();
        }

        [Fact]
        public async Task LastActiveDate_UpdatesCorrectlyOnSessionValidation()
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var user = new User
            {
                AuctionId = 1,
                DisplayName = "Test User",
                SessionToken = "session-token-123",
                LastActiveDate = DateTime.UtcNow.AddHours(-1)
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var originalLastActiveDate = user.LastActiveDate;

            // Act - Simulate session validation update
            var userToUpdate = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.SessionToken == "session-token-123");
            
            userToUpdate!.LastActiveDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Assert
            var updatedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId);
            updatedUser!.LastActiveDate.Should().BeAfter(originalLastActiveDate);
        }

        [Theory]
        [InlineData("AuctionMaster")]
        [InlineData("TeamCoach")]
        [InlineData("ProxyCoach")]
        [InlineData("Viewer")]
        public async Task UsersWithRoles_MaintainSessionTokens(string roleName)
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var sessionToken = $"token-for-{roleName.ToLower()}";
            var user = new User
            {
                AuctionId = 1,
                DisplayName = $"{roleName} User",
                SessionToken = sessionToken,
                IsConnected = true
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(); // Save user first to get UserId

            var userRole = new UserRole
            {
                UserId = user.UserId,
                Role = roleName,
                AssignedDate = DateTime.UtcNow
            };

            _dbContext.UserRoles.Add(userRole);
            await _dbContext.SaveChangesAsync();

            // Act
            var userWithRole = await _dbContext.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken);

            // Assert
            userWithRole.Should().NotBeNull();
            userWithRole!.SessionToken.Should().Be(sessionToken);
            userWithRole.UserRoles.Should().HaveCount(1);
            userWithRole.UserRoles.First().Role.Should().Be(roleName);
        }

        [Fact]
        public async Task MultipleAuctions_IsolateSessionTokens()
        {
            // Arrange
            var auction1 = new Auction
            {
                JoinCode = "AUCTION1",
                Name = "First Auction",
                MasterRecoveryCode = "MASTER1"
            };

            var auction2 = new Auction
            {
                JoinCode = "AUCTION2", 
                Name = "Second Auction",
                MasterRecoveryCode = "MASTER2"
            };

            var user1 = new User
            {
                AuctionId = 1,
                DisplayName = "User in Auction 1",
                SessionToken = "token-auction-1"
            };

            var user2 = new User
            {
                AuctionId = 2,
                DisplayName = "User in Auction 2",
                SessionToken = "token-auction-2"
            };

            _dbContext.Auctions.AddRange(auction1, auction2);
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            // Act & Assert - Auction 1 user validation
            var auction1User = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.AuctionId == 1 && u.SessionToken == "token-auction-1");
            auction1User.Should().NotBeNull();
            auction1User!.DisplayName.Should().Be("User in Auction 1");

            // Act & Assert - Auction 2 user validation  
            var auction2User = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.AuctionId == 2 && u.SessionToken == "token-auction-2");
            auction2User.Should().NotBeNull();
            auction2User!.DisplayName.Should().Be("User in Auction 2");

            // Act & Assert - Cross-auction validation should fail
            var crossAuctionUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.AuctionId == 1 && u.SessionToken == "token-auction-2");
            crossAuctionUser.Should().BeNull();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}