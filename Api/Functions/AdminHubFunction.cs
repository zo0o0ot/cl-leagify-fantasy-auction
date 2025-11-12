using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using Microsoft.Azure.Functions.Worker.Extensions.SignalRService;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Functions for Auction Master admin operations and real-time notifications.
/// Handles reconnection approvals, auction control, and admin-specific SignalR broadcasts.
/// </summary>
public class AdminHubFunction
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<AdminHubFunction> _logger;

    public AdminHubFunction(LeagifyAuctionDbContext context, ILogger<AdminHubFunction> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Request reconnection approval from Auction Master.
    /// Called when a user with an existing session tries to rejoin.
    /// </summary>
    [Function("RequestReconnection")]
    public async Task<ReconnectionResponse> RequestReconnection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/request-reconnection")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Reconnection request for auction {AuctionId}", auctionId);

            // Validate session token
            var user = await ValidateSessionToken(req, auctionId);
            if (user == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return new ReconnectionResponse { HttpResponse = unauthorizedResponse };
            }

            // Mark user as pending reconnection
            user.IsReconnectionPending = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {DisplayName} (ID: {UserId}) requesting reconnection approval",
                user.DisplayName, user.UserId);

            // Notify admins via SignalR
            var signalRMessage = new SignalRMessageAction("AdminNotifyReconnectionRequest")
            {
                GroupName = $"admin-{auctionId}",
                Arguments = new object[]
                {
                    new
                    {
                        UserId = user.UserId,
                        DisplayName = user.DisplayName,
                        RequestedAt = DateTime.UtcNow,
                        LastActiveDate = user.LastActiveDate
                    }
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "Reconnection request sent to Auction Master",
                IsPending = true
            });

            return new ReconnectionResponse
            {
                HttpResponse = response,
                SignalRMessages = new[] { signalRMessage }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting reconnection for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new ReconnectionResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Approve a user's reconnection request (Auction Master only).
    /// </summary>
    [Function("ApproveReconnection")]
    public async Task<AdminActionResponse> ApproveReconnection(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/approve-reconnection")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Reconnection approval request for auction {AuctionId}", auctionId);

            // Validate Auction Master authentication
            var admin = await ValidateAuctionMaster(req, auctionId);
            if (admin == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return new AdminActionResponse { HttpResponse = unauthorizedResponse };
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var approvalRequest = JsonSerializer.Deserialize<ApprovalRequest>(requestBody ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (approvalRequest == null || approvalRequest.UserId <= 0)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request: UserId required");
                return new AdminActionResponse { HttpResponse = badRequest };
            }

            // Find user requesting reconnection
            var reconnectingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == approvalRequest.UserId && u.AuctionId == auctionId);

            if (reconnectingUser == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("User not found");
                return new AdminActionResponse { HttpResponse = notFound };
            }

            // Approve reconnection
            reconnectingUser.IsReconnectionPending = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Auction Master {AdminName} approved reconnection for {UserName}",
                admin.DisplayName, reconnectingUser.DisplayName);

            // Build SignalR messages
            var signalRMessages = new List<SignalRMessageAction>
            {
                // Notify the reconnecting user
                new SignalRMessageAction("ReconnectionApproved")
                {
                    UserId = reconnectingUser.UserId.ToString(),
                    Arguments = new object[]
                    {
                        new
                        {
                            ApprovedBy = admin.DisplayName,
                            ApprovedAt = DateTime.UtcNow
                        }
                    }
                },
                // Notify all admins
                new SignalRMessageAction("AdminNotifyReconnectionApproved")
                {
                    GroupName = $"admin-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            UserId = reconnectingUser.UserId,
                            DisplayName = reconnectingUser.DisplayName,
                            ApprovedBy = admin.DisplayName,
                            ApprovedAt = DateTime.UtcNow
                        }
                    }
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = $"Reconnection approved for {reconnectingUser.DisplayName}"
            });

            return new AdminActionResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving reconnection for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new AdminActionResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Manually end current bidding (Auction Master only).
    /// </summary>
    [Function("EndCurrentBid")]
    public async Task<AdminActionResponse> EndCurrentBid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/end-bidding")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("End bidding request for auction {AuctionId}", auctionId);

            // Validate Auction Master authentication
            var admin = await ValidateAuctionMaster(req, auctionId);
            if (admin == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return new AdminActionResponse { HttpResponse = unauthorizedResponse };
            }

            _logger.LogInformation("Auction Master {AdminName} manually ended bidding for auction {AuctionId}",
                admin.DisplayName, auctionId);

            // Broadcast to all auction participants
            var signalRMessage = new SignalRMessageAction("BiddingEnded")
            {
                GroupName = $"auction-{auctionId}",
                Arguments = new object[]
                {
                    new
                    {
                        EndedBy = admin.DisplayName,
                        EndedAt = DateTime.UtcNow,
                        Reason = "Manual end by Auction Master"
                    }
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "Bidding ended successfully"
            });

            return new AdminActionResponse
            {
                HttpResponse = response,
                SignalRMessages = new[] { signalRMessage }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending bidding for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new AdminActionResponse { HttpResponse = errorResponse };
        }
    }

    // Helper methods

    private async Task<User?> ValidateSessionToken(HttpRequestData req, int auctionId)
    {
        if (!req.Headers.TryGetValues("X-Auction-Token", out var tokenValues))
        {
            return null;
        }

        var sessionToken = tokenValues.FirstOrDefault();
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AuctionId == auctionId && u.SessionToken == sessionToken);

        return user;
    }

    private async Task<User?> ValidateAuctionMaster(HttpRequestData req, int auctionId)
    {
        var user = await ValidateSessionToken(req, auctionId);
        if (user == null)
        {
            return null;
        }

        // Check if user is Auction Master
        var isAuctionMaster = await _context.UserRoles
            .AnyAsync(r => r.UserId == user.UserId && r.Role == "AuctionMaster");

        return isAuctionMaster ? user : null;
    }

    // Request/Response DTOs

    private class ApprovalRequest
    {
        public int UserId { get; set; }
    }

    public class ReconnectionResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class AdminActionResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }
}
