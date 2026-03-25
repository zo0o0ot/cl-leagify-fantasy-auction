using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Functions;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Services;
using System.Net;

namespace LeagifyFantasyAuction.Tests.Functions;

/// <summary>
/// Unit tests for AdminHubFunction class.
/// Tests auction master operations including reconnection approvals and bidding control.
/// </summary>
public class AdminHubFunctionTests : IDisposable
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly Mock<ILogger<AdminHubFunction>> _mockLogger;
    private readonly Mock<IBiddingService> _mockBiddingService;
    private readonly AdminHubFunction _function;
    private readonly DbContextOptions<LeagifyAuctionDbContext> _options;

    public AdminHubFunctionTests()
    {
        // Use in-memory database for testing
        _options = new DbContextOptionsBuilder<LeagifyAuctionDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new LeagifyAuctionDbContext(_options);
        _mockLogger = new Mock<ILogger<AdminHubFunction>>();
        _mockBiddingService = new Mock<IBiddingService>();
        _function = new AdminHubFunction(_context, _mockLogger.Object, _mockBiddingService.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task RequestReconnection_WithValidToken_ShouldMarkUserAsPending()
    {
        // Arrange
        var auction = CreateTestAuction();
        var user = CreateTestUser(auction.AuctionId, "TestUser", "valid-token");

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var mockRequest = CreateMockHttpRequest("valid-token");

        // Act
        var result = await _function.RequestReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.OK);
        result.SignalRMessages.Should().NotBeNull();
        result.SignalRMessages.Should().HaveCount(1);

        // Verify database state
        var updatedUser = await _context.Users.FindAsync(user.UserId);
        updatedUser.Should().NotBeNull();
        updatedUser!.IsReconnectionPending.Should().BeTrue();

        // Verify SignalR message
        var signalRMsg = result.SignalRMessages![0];
        signalRMsg.Target.Should().Be("AdminNotifyReconnectionRequest");
        signalRMsg.GroupName.Should().Be($"admin-{auction.AuctionId}");
    }

    [Fact]
    public async Task RequestReconnection_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var auction = CreateTestAuction();
        await _context.Auctions.AddAsync(auction);
        await _context.SaveChangesAsync();

        var mockRequest = CreateMockHttpRequest("invalid-token");

        // Act
        var result = await _function.RequestReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.SignalRMessages.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task RequestReconnection_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var auction = CreateTestAuction();
        await _context.Auctions.AddAsync(auction);
        await _context.SaveChangesAsync();

        var mockRequest = CreateMockHttpRequest(null);

        // Act
        var result = await _function.RequestReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApproveReconnection_WithValidAuctionMaster_ShouldApproveUser()
    {
        // Arrange
        var auction = CreateTestAuction();
        var auctionMaster = CreateTestUser(auction.AuctionId, "MasterUser", "master-token");
        var reconnectingUser = CreateTestUser(auction.AuctionId, "ReconnectingUser", "user-token");
        reconnectingUser.IsReconnectionPending = true;

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddRangeAsync(auctionMaster, reconnectingUser);
        await _context.SaveChangesAsync();

        var masterRole = new UserRole
        {
            UserId = auctionMaster.UserId,
            Role = "AuctionMaster",
            AssignedDate = DateTime.UtcNow
        };

        await _context.UserRoles.AddAsync(masterRole);
        await _context.SaveChangesAsync();

        var requestBody = $"{{\"userId\": {reconnectingUser.UserId}}}";
        var mockRequest = CreateMockHttpRequest("master-token", requestBody);

        // Act
        var result = await _function.ApproveReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.OK);
        result.SignalRMessages.Should().NotBeNull();
        result.SignalRMessages.Should().HaveCount(2); // One to user, one to admin group

        // Verify database state
        var updatedUser = await _context.Users.FindAsync(reconnectingUser.UserId);
        updatedUser.Should().NotBeNull();
        updatedUser!.IsReconnectionPending.Should().BeFalse();

        // Verify SignalR messages
        result.SignalRMessages.Should().Contain(m => m.Target == "ReconnectionApproved");
        result.SignalRMessages.Should().Contain(m => m.Target == "AdminNotifyReconnectionApproved");
    }

    [Fact]
    public async Task ApproveReconnection_WithNonMasterUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var auction = CreateTestAuction();
        var regularUser = CreateTestUser(auction.AuctionId, "RegularUser", "user-token");
        var reconnectingUser = CreateTestUser(auction.AuctionId, "ReconnectingUser", "reconnect-token");
        reconnectingUser.IsReconnectionPending = true;

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddRangeAsync(regularUser, reconnectingUser);
        await _context.SaveChangesAsync();

        var requestBody = $"{{\"userId\": {reconnectingUser.UserId}}}";
        var mockRequest = CreateMockHttpRequest("user-token", requestBody);

        // Act
        var result = await _function.ApproveReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.SignalRMessages.Should().BeNullOrEmpty();

        // Verify user is still pending
        var user = await _context.Users.FindAsync(reconnectingUser.UserId);
        user!.IsReconnectionPending.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveReconnection_WithInvalidUserId_ShouldReturnBadRequest()
    {
        // Arrange
        var auction = CreateTestAuction();
        var auctionMaster = CreateTestUser(auction.AuctionId, "MasterUser", "master-token");

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddAsync(auctionMaster);
        await _context.SaveChangesAsync();

        var masterRole = new UserRole
        {
            UserId = auctionMaster.UserId,
            Role = "AuctionMaster",
            AssignedDate = DateTime.UtcNow
        };
        await _context.UserRoles.AddAsync(masterRole);
        await _context.SaveChangesAsync();

        var requestBody = "{\"userId\": 0}"; // Invalid userId
        var mockRequest = CreateMockHttpRequest("master-token", requestBody);

        // Act
        var result = await _function.ApproveReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApproveReconnection_WithNonexistentUser_ShouldReturnNotFound()
    {
        // Arrange
        var auction = CreateTestAuction();
        var auctionMaster = CreateTestUser(auction.AuctionId, "MasterUser", "master-token");

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddAsync(auctionMaster);
        await _context.SaveChangesAsync();

        var masterRole = new UserRole
        {
            UserId = auctionMaster.UserId,
            Role = "AuctionMaster",
            AssignedDate = DateTime.UtcNow
        };
        await _context.UserRoles.AddAsync(masterRole);
        await _context.SaveChangesAsync();

        var requestBody = "{\"userId\": 99999}"; // Non-existent user
        var mockRequest = CreateMockHttpRequest("master-token", requestBody);

        // Act
        var result = await _function.ApproveReconnection(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndCurrentBid_WithValidAuctionMaster_ShouldCompleteAndBroadcast()
    {
        // Arrange
        var auction = CreateTestAuction();
        var auctionMaster = CreateTestUser(auction.AuctionId, "MasterUser", "master-token");

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddAsync(auctionMaster);
        await _context.SaveChangesAsync();

        var masterRole = new UserRole
        {
            UserId = auctionMaster.UserId,
            Role = "AuctionMaster",
            AssignedDate = DateTime.UtcNow
        };

        await _context.UserRoles.AddAsync(masterRole);
        await _context.SaveChangesAsync();

        // Setup mock bidding service to return success
        _mockBiddingService.Setup(s => s.CompleteBiddingAsync(It.IsAny<int>()))
            .ReturnsAsync(new CompleteBiddingResult
            {
                Success = true,
                DraftPickId = 1,
                SchoolName = "Test School",
                WinningBid = 10.00m,
                WinnerUserId = auctionMaster.UserId,
                WinnerDisplayName = "MasterUser",
                TeamId = 1,
                TeamName = "Team 1",
                NextNominatorUserId = null,
                IsAuctionComplete = false
            });

        var mockRequest = CreateMockHttpRequest("master-token");

        // Act
        var result = await _function.EndCurrentBid(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.OK);
        result.SignalRMessages.Should().NotBeNull();
        result.SignalRMessages.Should().HaveCountGreaterThanOrEqualTo(1);

        // Verify SignalR message contains BiddingCompleted
        var signalRMsg = result.SignalRMessages![0];
        signalRMsg.Target.Should().Be("BiddingCompleted");
        signalRMsg.GroupName.Should().Be($"auction-{auction.AuctionId}");
    }

    [Fact]
    public async Task EndCurrentBid_WithNonMasterUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var auction = CreateTestAuction();
        var regularUser = CreateTestUser(auction.AuctionId, "RegularUser", "user-token");

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddAsync(regularUser);
        await _context.SaveChangesAsync();

        var mockRequest = CreateMockHttpRequest("user-token");

        // Act
        var result = await _function.EndCurrentBid(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.SignalRMessages.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task EndCurrentBid_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var auction = CreateTestAuction();
        await _context.Auctions.AddAsync(auction);
        await _context.SaveChangesAsync();

        var mockRequest = CreateMockHttpRequest(null);

        // Act
        var result = await _function.EndCurrentBid(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndCurrentBid_WithNoActiveBidding_ShouldReturnBadRequest()
    {
        // Arrange
        var auction = CreateTestAuction();
        var auctionMaster = CreateTestUser(auction.AuctionId, "MasterUser", "master-token");

        await _context.Auctions.AddAsync(auction);
        await _context.Users.AddAsync(auctionMaster);
        await _context.SaveChangesAsync();

        var masterRole = new UserRole
        {
            UserId = auctionMaster.UserId,
            Role = "AuctionMaster",
            AssignedDate = DateTime.UtcNow
        };

        await _context.UserRoles.AddAsync(masterRole);
        await _context.SaveChangesAsync();

        // Setup mock bidding service to return failure (no active bidding)
        _mockBiddingService.Setup(s => s.CompleteBiddingAsync(It.IsAny<int>()))
            .ReturnsAsync(new CompleteBiddingResult
            {
                Success = false,
                ErrorMessage = "No active bidding to complete"
            });

        var mockRequest = CreateMockHttpRequest("master-token");

        // Act
        var result = await _function.EndCurrentBid(mockRequest.Object, auction.AuctionId);

        // Assert
        result.Should().NotBeNull();
        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Helper methods

    private static Auction CreateTestAuction()
    {
        return new Auction
        {
            AuctionId = 1,
            Name = "Test Auction",
            JoinCode = "TEST123",
            MasterRecoveryCode = "RECOVER123",
            Status = "Draft",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };
    }

    private static User CreateTestUser(int auctionId, string displayName, string sessionToken)
    {
        return new User
        {
            AuctionId = auctionId,
            DisplayName = displayName,
            SessionToken = sessionToken,
            IsConnected = false,
            IsReconnectionPending = false,
            JoinedDate = DateTime.UtcNow,
            LastActiveDate = DateTime.UtcNow
        };
    }

    private static Mock<HttpRequestData> CreateMockHttpRequest(string? sessionToken, string? body = null)
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var workerOptions = new WorkerOptions { Serializer = new Azure.Core.Serialization.JsonObjectSerializer(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) };
        var optionsMock = Microsoft.Extensions.Options.Options.Create(workerOptions);
        mockServiceProvider.Setup(x => x.GetService(typeof(Microsoft.Extensions.Options.IOptions<WorkerOptions>))).Returns(optionsMock);

        var contextMock = new Mock<FunctionContext>();
        contextMock.Setup(c => c.InstanceServices).Returns(mockServiceProvider.Object);

        var mockRequest = new Mock<HttpRequestData>(MockBehavior.Strict, contextMock.Object);

        var headers = new HttpHeadersCollection();
        if (sessionToken != null)
        {
            headers.Add("X-Auction-Token", sessionToken);
        }

        mockRequest.Setup(r => r.Headers).Returns(headers);

        if (body != null)
        {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
            mockRequest.Setup(r => r.Body).Returns(stream);
        }

        mockRequest.Setup(r => r.CreateResponse())
            .Returns(() =>
            {
                var mockResponse = new Mock<HttpResponseData>(MockBehavior.Strict, contextMock.Object);
                mockResponse.SetupProperty(r => r.StatusCode);
                mockResponse.SetupProperty(r => r.Headers, new HttpHeadersCollection());
                mockResponse.SetupProperty(r => r.Body, new MemoryStream());
                return mockResponse.Object;
            });

        return mockRequest;
    }
}
