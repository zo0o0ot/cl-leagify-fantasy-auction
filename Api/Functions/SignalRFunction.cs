using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Functions for SignalR Service integration in Static Web Apps.
/// Provides connection management and real-time messaging for auction participants.
/// </summary>
public class SignalRFunction(ILoggerFactory loggerFactory, LeagifyAuctionDbContext context)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SignalRFunction>();

    /// <summary>
    /// Negotiate SignalR connection for auction participants.
    /// Returns connection information for the client to connect to Azure SignalR Service.
    /// Uses SignalR input binding to automatically generate connection info with access token.
    /// </summary>
    [Function("negotiate")]
    public HttpResponseData Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/negotiate")] HttpRequestData req,
        [SignalRConnectionInfoInput(HubName = "auctionhub")] string connectionInfo)
    {
        _logger.LogInformation("SignalR negotiate endpoint called - returning connection info");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(connectionInfo);

        return response;
    }

    /// <summary>
    /// Handle user connection events and update database status.
    /// Called when a user successfully connects to the SignalR hub.
    /// </summary>
    [Function("OnConnected")]
    public async Task<HttpResponseData> OnConnected(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var connectionEvent = JsonSerializer.Deserialize<ConnectionEvent>(requestBody);

            if (connectionEvent?.UserId == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid connection event");
            }

            // Update user connection status in database
            var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == connectionEvent.UserId);
            if (user != null)
            {
                user.IsConnected = true;
                user.LastActiveDate = DateTime.UtcNow;
                user.ConnectionId = connectionEvent.ConnectionId;
                
                await context.SaveChangesAsync();
                
                _logger.LogInformation("User {UserId} connected to auction {AuctionId}", 
                    user.UserId, user.AuctionId);

                // Notify other participants that user connected
                await BroadcastUserStatusUpdate(user.AuctionId, user.UserId, user.DisplayName, true);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user connection");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Connection handling failed");
        }
    }

    /// <summary>
    /// Handle user disconnection events and update database status.
    /// Called when a user disconnects from the SignalR hub.
    /// </summary>
    [Function("OnDisconnected")]
    public async Task<HttpResponseData> OnDisconnected(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var disconnectionEvent = JsonSerializer.Deserialize<DisconnectionEvent>(requestBody);

            if (string.IsNullOrEmpty(disconnectionEvent?.ConnectionId))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid disconnection event");
            }

            // Find user by connection ID and update status
            var user = await context.Users.FirstOrDefaultAsync(u => u.ConnectionId == disconnectionEvent.ConnectionId);
            if (user != null)
            {
                user.IsConnected = false;
                user.ConnectionId = null;
                
                await context.SaveChangesAsync();
                
                _logger.LogInformation("User {UserId} disconnected from auction {AuctionId}", 
                    user.UserId, user.AuctionId);

                // Notify other participants that user disconnected
                await BroadcastUserStatusUpdate(user.AuctionId, user.UserId, user.DisplayName, false);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user disconnection");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Disconnection handling failed");
        }
    }

    /// <summary>
    /// Send a message to all participants in an auction group via SignalR.
    /// Used for broadcasting auction events and status updates in real-time.
    /// </summary>
    [Function("BroadcastToAuction")]
    public async Task<HttpResponseData> BroadcastToAuction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/broadcast")] HttpRequestData req,
        [SignalROutput(HubName = "auctionhub")] IAsyncCollector<SignalRMessageAction> signalRMessages)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var broadcastRequest = JsonSerializer.Deserialize<BroadcastRequest>(requestBody);

            if (broadcastRequest?.AuctionId == null || string.IsNullOrEmpty(broadcastRequest.EventName))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid broadcast request");
            }

            _logger.LogInformation("Broadcasting {EventName} to auction {AuctionId}",
                broadcastRequest.EventName, broadcastRequest.AuctionId);

            // Broadcast message to the specified group (or all if no group specified)
            var message = new SignalRMessageAction(broadcastRequest.EventName)
            {
                Arguments = broadcastRequest.Arguments ?? Array.Empty<object>()
            };

            if (!string.IsNullOrEmpty(broadcastRequest.GroupName))
            {
                message.GroupName = broadcastRequest.GroupName;
            }

            await signalRMessages.AddAsync(message);

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting to auction");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Broadcast failed");
        }
    }

    /// <summary>
    /// Add a user's connection to a SignalR group (e.g., waiting room, auction group).
    /// Called when a user joins the waiting room or auction.
    /// </summary>
    [Function("AddToGroup")]
    public async Task<HttpResponseData> AddToGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/groups/add")] HttpRequestData req,
        [SignalROutput(HubName = "auctionhub")] IAsyncCollector<SignalRGroupAction> signalRGroupActions)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var groupRequest = JsonSerializer.Deserialize<GroupManagementRequest>(requestBody);

            if (groupRequest == null || string.IsNullOrEmpty(groupRequest.ConnectionId) || string.IsNullOrEmpty(groupRequest.GroupName))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid group request");
            }

            _logger.LogInformation("Adding connection {ConnectionId} to group {GroupName}",
                groupRequest.ConnectionId, groupRequest.GroupName);

            await signalRGroupActions.AddAsync(new SignalRGroupAction(SignalRGroupActionType.Add)
            {
                GroupName = groupRequest.GroupName,
                ConnectionId = groupRequest.ConnectionId
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding connection to group");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to add to group");
        }
    }

    /// <summary>
    /// Remove a user's connection from a SignalR group.
    /// Called when a user leaves the waiting room or disconnects.
    /// </summary>
    [Function("RemoveFromGroup")]
    public async Task<HttpResponseData> RemoveFromGroup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signalr/groups/remove")] HttpRequestData req,
        [SignalROutput(HubName = "auctionhub")] IAsyncCollector<SignalRGroupAction> signalRGroupActions)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var groupRequest = JsonSerializer.Deserialize<GroupManagementRequest>(requestBody);

            if (groupRequest == null || string.IsNullOrEmpty(groupRequest.ConnectionId) || string.IsNullOrEmpty(groupRequest.GroupName))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid group request");
            }

            _logger.LogInformation("Removing connection {ConnectionId} from group {GroupName}",
                groupRequest.ConnectionId, groupRequest.GroupName);

            await signalRGroupActions.AddAsync(new SignalRGroupAction(SignalRGroupActionType.Remove)
            {
                GroupName = groupRequest.GroupName,
                ConnectionId = groupRequest.ConnectionId
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing connection from group");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to remove from group");
        }
    }

    /// <summary>
    /// Get real-time connection status for all auction participants.
    /// Used by Auction Masters to monitor participant connectivity.
    /// </summary>
    [Function("GetConnectionStatus")]
    public async Task<HttpResponseData> GetConnectionStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "signalr/auctions/{auctionId:int}/status")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            // Get all participants and their connection status
            var participants = await context.Users
                .Where(u => u.AuctionId == auctionId)
                .Select(u => new ParticipantStatus
                {
                    UserId = u.UserId,
                    DisplayName = u.DisplayName,
                    IsConnected = u.IsConnected,
                    LastActiveDate = u.LastActiveDate,
                    ConnectionId = u.ConnectionId
                })
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(participants);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection status for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to get connection status");
        }
    }

    // Helper methods

    private async Task<bool> ValidateManagementAuth(HttpRequestData req)
    {
        var token = req.Headers.FirstOrDefault(h => h.Key == "X-Management-Token").Value?.FirstOrDefault();
        return !string.IsNullOrEmpty(token) && token == Environment.GetEnvironmentVariable("MANAGEMENT_PASSWORD");
    }

    private static string ExtractUrlFromConnectionString(string connectionString)
    {
        // Extract the endpoint URL from Azure SignalR connection string
        var endpointPart = connectionString.Split(';')
            .FirstOrDefault(part => part.StartsWith("Endpoint="));
        
        return endpointPart?.Substring("Endpoint=".Length) ?? "/api/signalr";
    }

    private async Task BroadcastUserStatusUpdate(int auctionId, int userId, string displayName, bool isConnected)
    {
        try
        {
            // TODO: Implement actual SignalR broadcasting
            // This would use Azure SignalR Service to broadcast to all auction participants
            _logger.LogInformation("User {DisplayName} (ID: {UserId}) {Status} in auction {AuctionId}", 
                displayName, userId, isConnected ? "connected" : "disconnected", auctionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting user status update");
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }

    // Data models for SignalR events
    private class ConnectionEvent
    {
        public int UserId { get; set; }
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    private class DisconnectionEvent
    {
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private class BroadcastRequest
    {
        public int AuctionId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public object? Data { get; set; }
        public object[]? Arguments { get; set; }
        public string? GroupName { get; set; }
        public string[]? UserIds { get; set; }
    }

    private class GroupManagementRequest
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public int? AuctionId { get; set; }
        public int? UserId { get; set; }
    }

    private class ParticipantStatus
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public DateTime LastActiveDate { get; set; }
        public string? ConnectionId { get; set; }
    }
}