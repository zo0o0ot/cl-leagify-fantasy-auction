using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using Microsoft.EntityFrameworkCore;

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
    /// </summary>
    [Function("negotiate")]
    public async Task<HttpResponseData> Negotiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            // Extract user session information from request headers or body
            var sessionToken = req.Headers.FirstOrDefault(h => h.Key == "X-Auction-Token").Value?.FirstOrDefault();
            
            if (string.IsNullOrEmpty(sessionToken))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Session token required");
            }

            // Validate session and get user information
            var user = await ValidateSessionToken(sessionToken);
            if (user == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Invalid session token");
            }

            // For now, create a basic connection info response
            // In a real implementation, this would use Azure SignalR Service bindings
            var connectionInfo = new
            {
                url = GetSignalRUrl(),
                accessToken = GenerateAccessToken(user),
                userId = user.UserId.ToString(),
                auctionId = user.AuctionId.ToString()
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(connectionInfo);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SignalR negotiation");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Negotiation failed");
        }
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
    /// Send a message to all participants in an auction.
    /// Used for broadcasting auction events and status updates.
    /// </summary>
    [Function("BroadcastToAuction")]
    public async Task<HttpResponseData> BroadcastToAuction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var broadcastRequest = JsonSerializer.Deserialize<BroadcastRequest>(requestBody);

            if (broadcastRequest?.AuctionId == null || string.IsNullOrEmpty(broadcastRequest.EventName))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid broadcast request");
            }

            // TODO: Implement actual SignalR broadcasting using Azure SignalR Service
            // For now, just log the event
            _logger.LogInformation("Broadcasting {EventName} to auction {AuctionId}", 
                broadcastRequest.EventName, broadcastRequest.AuctionId);

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

    private async Task<User?> ValidateSessionToken(string sessionToken)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.IsConnected);
    }

    private async Task<bool> ValidateManagementAuth(HttpRequestData req)
    {
        var token = req.Headers.FirstOrDefault(h => h.Key == "X-Management-Token").Value?.FirstOrDefault();
        return !string.IsNullOrEmpty(token) && token == Environment.GetEnvironmentVariable("MANAGEMENT_PASSWORD");
    }

    private static string GetSignalRUrl()
    {
        // In a real implementation, this would come from Azure SignalR Service configuration
        var signalrConnectionString = Environment.GetEnvironmentVariable("AzureSignalRConnectionString");
        return string.IsNullOrEmpty(signalrConnectionString) 
            ? "/api/signalr" // Fallback for development
            : ExtractUrlFromConnectionString(signalrConnectionString);
    }

    private static string ExtractUrlFromConnectionString(string connectionString)
    {
        // Extract the endpoint URL from Azure SignalR connection string
        var endpointPart = connectionString.Split(';')
            .FirstOrDefault(part => part.StartsWith("Endpoint="));
        
        return endpointPart?.Substring("Endpoint=".Length) ?? "/api/signalr";
    }

    private static string GenerateAccessToken(User user)
    {
        // In a real implementation, this would generate a proper JWT token for SignalR
        // For now, return the session token
        return user.SessionToken ?? string.Empty;
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
        public string[]? UserIds { get; set; }
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