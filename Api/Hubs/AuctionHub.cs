using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Data;

namespace LeagifyFantasyAuction.Api.Hubs;

/// <summary>
/// SignalR hub for real-time auction communication.
/// Handles user connections, group management, and live auction events.
/// </summary>
public class AuctionHub : Hub
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(LeagifyAuctionDbContext context, ILogger<AuctionHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Group names for different auction permissions
    private static string GetAuctionGroup(int auctionId) => $"auction-{auctionId}";
    private static string GetAdminGroup(int auctionId) => $"admin-{auctionId}";
    private static string GetWaitingRoomGroup(int auctionId) => $"waiting-{auctionId}";

    /// <summary>
    /// Join an auction group when user connects
    /// </summary>
    public async Task JoinAuction(int auctionId, string displayName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetAuctionGroup(auctionId));

        // Notify other users that someone joined
        await Clients.Group(GetAuctionGroup(auctionId))
            .SendAsync("UserJoined", displayName, Context.ConnectionId);
    }

    /// <summary>
    /// Join waiting room group for pre-auction lobby
    /// </summary>
    public async Task JoinWaitingRoom(int auctionId, string displayName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetWaitingRoomGroup(auctionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GetAuctionGroup(auctionId));

        // Notify other users in waiting room that someone joined
        await Clients.OthersInGroup(GetWaitingRoomGroup(auctionId))
            .SendAsync("UserJoinedWaitingRoom", displayName);
    }

    /// <summary>
    /// Broadcast a test bid to all waiting room participants
    /// Called from WaitingRoomFunction after successful bid placement
    /// </summary>
    public async Task BroadcastTestBid(int auctionId, string bidderName, decimal amount)
    {
        await Clients.Group(GetWaitingRoomGroup(auctionId))
            .SendAsync("TestBidPlaced", bidderName, amount, DateTime.UtcNow);
    }

    /// <summary>
    /// Broadcast readiness status update to all waiting room participants
    /// </summary>
    public async Task BroadcastReadinessUpdate(int auctionId, string displayName, bool isReady)
    {
        await Clients.Group(GetWaitingRoomGroup(auctionId))
            .SendAsync("ReadinessUpdated", displayName, isReady);
    }

    /// <summary>
    /// Broadcast that a user tested bidding for the first time
    /// </summary>
    public async Task BroadcastFirstTestBid(int auctionId, string displayName)
    {
        await Clients.Group(GetWaitingRoomGroup(auctionId))
            .SendAsync("UserTestedBidding", displayName);
    }

    /// <summary>
    /// Join admin group for Auction Master
    /// </summary>
    public async Task JoinAsAdmin(int auctionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetAdminGroup(auctionId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GetAuctionGroup(auctionId));
        
        // Admin gets all auction updates plus admin-specific ones
        await Clients.Group(GetAdminGroup(auctionId))
            .SendAsync("AdminConnected", Context.ConnectionId);
    }

    /// <summary>
    /// Place a bid on the currently nominated school
    /// </summary>
    public async Task PlaceBid(int auctionId, int schoolId, decimal amount)
    {
        // TODO: Implement bid validation and processing
        // For now, just broadcast the bid
        await Clients.Group(GetAuctionGroup(auctionId))
            .SendAsync("BidPlaced", schoolId, amount, Context.ConnectionId);
    }

    /// <summary>
    /// Nominate a school for bidding (auto-bids $1)
    /// </summary>
    public async Task NominateSchool(int auctionId, int schoolId)
    {
        // TODO: Implement nomination logic
        await Clients.Group(GetAuctionGroup(auctionId))
            .SendAsync("SchoolNominated", schoolId, Context.ConnectionId);
    }

    /// <summary>
    /// Pass on current school being bid on
    /// </summary>
    public async Task PassOnSchool(int auctionId)
    {
        // TODO: Implement pass logic
        await Clients.Group(GetAuctionGroup(auctionId))
            .SendAsync("PlayerPassed", Context.ConnectionId);
    }

    /// <summary>
    /// Auction Master approves user reconnection request
    /// </summary>
    public async Task ApproveReconnection(int auctionId, int userId)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null || !await IsAuctionMaster(currentUser, auctionId))
            {
                _logger.LogWarning("Unauthorized reconnection approval attempt");
                return;
            }

            // Find user requesting reconnection
            var reconnectingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.AuctionId == auctionId);

            if (reconnectingUser != null && reconnectingUser.IsReconnectionPending)
            {
                reconnectingUser.IsReconnectionPending = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Auction Master {MasterName} approved reconnection for {UserName}",
                    currentUser.DisplayName, reconnectingUser.DisplayName);

                // Notify the reconnecting user
                if (!string.IsNullOrEmpty(reconnectingUser.ConnectionId))
                {
                    await Clients.Client(reconnectingUser.ConnectionId)
                        .SendAsync("ReconnectionApproved");
                }

                // Notify admins
                await Clients.Group(GetAdminGroup(auctionId))
                    .SendAsync("AdminNotifyReconnectionApproved", new
                    {
                        UserId = userId,
                        DisplayName = reconnectingUser.DisplayName,
                        ApprovedBy = currentUser.DisplayName,
                        ApprovedAt = DateTime.UtcNow
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving reconnection for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Auction Master manually ends current bidding
    /// </summary>
    public async Task EndCurrentBid(int auctionId)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null || !await IsAuctionMaster(currentUser, auctionId))
            {
                _logger.LogWarning("Unauthorized attempt to end bidding");
                return;
            }

            _logger.LogInformation("Auction Master {MasterName} manually ended current bidding for auction {AuctionId}",
                currentUser.DisplayName, auctionId);

            // Broadcast to all auction participants
            await Clients.Group(GetAuctionGroup(auctionId))
                .SendAsync("BiddingEnded", new
                {
                    EndedBy = currentUser.DisplayName,
                    EndedAt = DateTime.UtcNow,
                    Reason = "Manual end by Auction Master"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending current bid for auction {AuctionId}", auctionId);
        }
    }

    /// <summary>
    /// Assign won school to a roster position
    /// </summary>
    public async Task AssignSchoolToPosition(int auctionId, int teamId, int schoolId, int positionId)
    {
        // TODO: Implement position assignment logic
        await Clients.Group(GetAuctionGroup(auctionId))
            .SendAsync("SchoolAssigned", teamId, schoolId, positionId);
    }

    /// <summary>
    /// Handle user disconnection - update database and notify participants
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Find user by connection ID
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);

            if (user != null)
            {
                _logger.LogInformation("User {DisplayName} (ID: {UserId}) disconnected from auction {AuctionId}",
                    user.DisplayName, user.UserId, user.AuctionId);

                // Update connection status
                user.IsConnected = false;
                user.ConnectionId = null;
                await _context.SaveChangesAsync();

                // Broadcast to all auction participants
                await Clients.Group(GetAuctionGroup(user.AuctionId))
                    .SendAsync("UserDisconnected", user.UserId, user.DisplayName, DateTime.UtcNow);

                // Broadcast to admin group with additional details
                await Clients.Group(GetAdminGroup(user.AuctionId))
                    .SendAsync("AdminNotifyDisconnection", new
                    {
                        UserId = user.UserId,
                        DisplayName = user.DisplayName,
                        DisconnectedAt = DateTime.UtcNow,
                        Reason = exception?.Message ?? "Normal disconnection"
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user disconnection for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Handle user connection with authentication and auto-join groups
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            // Get session token from query string
            var httpContext = Context.GetHttpContext();
            var sessionToken = httpContext?.Request.Query["sessionToken"].ToString();

            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogWarning("Connection attempt without session token: {ConnectionId}", Context.ConnectionId);
                await base.OnConnectedAsync();
                return;
            }

            // Find user by session token
            var user = await _context.Users
                .Include(u => u.Auction)
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken);

            if (user == null)
            {
                _logger.LogWarning("Connection attempt with invalid session token");
                await base.OnConnectedAsync();
                return;
            }

            _logger.LogInformation("User {DisplayName} (ID: {UserId}) connected to auction {AuctionId}",
                user.DisplayName, user.UserId, user.AuctionId);

            // Update connection status in database
            user.IsConnected = true;
            user.ConnectionId = Context.ConnectionId;
            user.LastActiveDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Auto-join appropriate groups based on auction status
            await Groups.AddToGroupAsync(Context.ConnectionId, GetAuctionGroup(user.AuctionId));

            // Join waiting room if auction is in Draft status
            if (user.Auction.Status == "Draft")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetWaitingRoomGroup(user.AuctionId));
            }

            // Join admin group if user is Auction Master
            var isAuctionMaster = user.UserRoles.Any(r => r.RoleType == "AuctionMaster");
            if (isAuctionMaster)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetAdminGroup(user.AuctionId));
                _logger.LogInformation("User {DisplayName} joined admin group for auction {AuctionId}",
                    user.DisplayName, user.AuctionId);
            }

            // Broadcast connection to all auction participants
            await Clients.Group(GetAuctionGroup(user.AuctionId))
                .SendAsync("UserConnected", user.UserId, user.DisplayName, DateTime.UtcNow);

            // Broadcast to admin group with additional details
            await Clients.Group(GetAdminGroup(user.AuctionId))
                .SendAsync("AdminNotifyConnection", new
                {
                    UserId = user.UserId,
                    DisplayName = user.DisplayName,
                    ConnectedAt = DateTime.UtcNow,
                    IsAuctionMaster = isAuctionMaster,
                    HasTestedBidding = user.HasTestedBidding,
                    IsReadyToDraft = user.IsReadyToDraft
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user connection for connection {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    // Helper methods for authentication and authorization

    /// <summary>
    /// Get the currently connected user based on their connection ID
    /// </summary>
    private async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user for connection {ConnectionId}", Context.ConnectionId);
            return null;
        }
    }

    /// <summary>
    /// Check if user has Auction Master role for the specified auction
    /// </summary>
    private async Task<bool> IsAuctionMaster(User user, int auctionId)
    {
        if (user.AuctionId != auctionId)
            return false;

        return user.UserRoles.Any(r => r.RoleType == "AuctionMaster");
    }

    /// <summary>
    /// Request reconnection approval from Auction Master
    /// Called when a user with existing session tries to rejoin
    /// </summary>
    public async Task RequestReconnection(int auctionId)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                _logger.LogWarning("Reconnection request from unauthenticated user");
                return;
            }

            // Mark user as pending reconnection
            currentUser.IsReconnectionPending = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {DisplayName} requested reconnection to auction {AuctionId}",
                currentUser.DisplayName, auctionId);

            // Notify admins of reconnection request
            await Clients.Group(GetAdminGroup(auctionId))
                .SendAsync("AdminNotifyReconnectionRequest", new
                {
                    UserId = currentUser.UserId,
                    DisplayName = currentUser.DisplayName,
                    RequestedAt = DateTime.UtcNow,
                    LastActiveDate = currentUser.LastActiveDate
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting reconnection for auction {AuctionId}", auctionId);
        }
    }
}