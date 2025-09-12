using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagifyFantasyAuction.Tests.Functions
{
    public class RoleManagementFunctionTests : IDisposable
    {
        private readonly LeagifyAuctionDbContext _dbContext;

        public RoleManagementFunctionTests()
        {
            // Create in-memory database for testing
            var options = new DbContextOptionsBuilder<LeagifyAuctionDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new LeagifyAuctionDbContext(options);
        }

        [Fact]
        public async Task UserRole_CanBeCreatedAndRetrieved()
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
                IsConnected = true
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var userRole = new UserRole
            {
                UserId = user.UserId,
                Role = "TeamCoach",
                TeamId = null,
                AssignedDate = DateTime.UtcNow
            };

            // Act
            _dbContext.UserRoles.Add(userRole);
            await _dbContext.SaveChangesAsync();

            // Assert
            var retrievedRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.UserId && ur.Role == "TeamCoach");
            
            Assert.NotNull(retrievedRole);
            Assert.Equal("TeamCoach", retrievedRole.Role);
            Assert.Equal(user.UserId, retrievedRole.UserId);
        }

        [Fact] 
        public async Task UserRole_CanBeDeletedFromDatabase()
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
                IsConnected = true
            };

            var userRole = new UserRole
            {
                UserId = user.UserId,
                Role = "TeamCoach",
                AssignedDate = DateTime.UtcNow
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            _dbContext.UserRoles.Add(userRole);
            await _dbContext.SaveChangesAsync();

            // Act
            _dbContext.UserRoles.Remove(userRole);
            await _dbContext.SaveChangesAsync();

            // Assert
            var deletedRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserRoleId == userRole.UserRoleId);
            Assert.Null(deletedRole);
        }

        [Fact]
        public async Task User_CascadeDeletesRoles()
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
                IsConnected = true
            };

            var userRole = new UserRole
            {
                UserId = user.UserId,
                Role = "TeamCoach", 
                AssignedDate = DateTime.UtcNow
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            _dbContext.UserRoles.Add(userRole);
            await _dbContext.SaveChangesAsync();

            // Act - Delete user should cascade to roles
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();

            // Assert
            var deletedUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId);
            var deletedRole = await _dbContext.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == user.UserId);
            
            Assert.Null(deletedUser);
            Assert.Null(deletedRole);
        }

        [Theory]
        [InlineData("AuctionMaster")]
        [InlineData("TeamCoach")]
        [InlineData("ProxyCoach")]
        [InlineData("Viewer")]
        public async Task UserRole_AcceptsValidRoleTypes(string roleName)
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
                IsConnected = true
            };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var userRole = new UserRole
            {
                UserId = user.UserId,
                Role = roleName,
                AssignedDate = DateTime.UtcNow
            };

            // Act
            _dbContext.UserRoles.Add(userRole);
            await _dbContext.SaveChangesAsync();

            // Assert
            var savedRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.UserId && ur.Role == roleName);
            
            Assert.NotNull(savedRole);
            Assert.Equal(roleName, savedRole.Role);
        }

        [Fact]
        public async Task Auction_CanHaveMultipleUsersWithRoles()
        {
            // Arrange
            var auction = new Auction
            {
                JoinCode = "TEST123",
                Name = "Test Auction",
                MasterRecoveryCode = "MASTER123"
            };

            var user1 = new User { AuctionId = 1, DisplayName = "User 1", IsConnected = true };
            var user2 = new User { AuctionId = 1, DisplayName = "User 2", IsConnected = true };

            _dbContext.Auctions.Add(auction);
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            var role1 = new UserRole { UserId = user1.UserId, Role = "AuctionMaster", AssignedDate = DateTime.UtcNow };
            var role2 = new UserRole { UserId = user2.UserId, Role = "TeamCoach", AssignedDate = DateTime.UtcNow };

            // Act
            _dbContext.UserRoles.AddRange(role1, role2);
            await _dbContext.SaveChangesAsync();

            // Assert
            var auctionUsers = await _dbContext.Users
                .Where(u => u.AuctionId == auction.AuctionId)
                .Include(u => u.UserRoles)
                .ToListAsync();

            Assert.Equal(2, auctionUsers.Count);
            Assert.Contains(auctionUsers, u => u.UserRoles.Any(r => r.Role == "AuctionMaster"));
            Assert.Contains(auctionUsers, u => u.UserRoles.Any(r => r.Role == "TeamCoach"));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dbContext.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}