using Microsoft.AspNetCore.SignalR;
using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Hubs;

public class AuctionHub : Hub
{
    // Group names for different auction permissions
    private static string GetAuctionGroup(int auctionId) => $"auction-{auctionId}";
    private static string GetAdminGroup(int auctionId) => $"admin-{auctionId}";

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
    /// Auction Master approves user reconnection
    /// </summary>
    public async Task ApproveReconnection(int auctionId, string userConnectionId)
    {
        // TODO: Implement reconnection approval logic
        await Clients.Group(GetAdminGroup(auctionId))
            .SendAsync("ReconnectionApproved", userConnectionId);
    }

    /// <summary>
    /// Auction Master manually ends current bidding
    /// </summary>
    public async Task EndCurrentBid(int auctionId)
    {
        // TODO: Implement bid ending logic
        await Clients.Group(GetAuctionGroup(auctionId))
            .SendAsync("BiddingEnded", Context.ConnectionId);
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
    /// Handle user disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // TODO: Update user connection status in database
        // TODO: Notify auction participants of disconnection
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Handle user connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // TODO: Update user connection status in database
        await base.OnConnectedAsync();
    }
}