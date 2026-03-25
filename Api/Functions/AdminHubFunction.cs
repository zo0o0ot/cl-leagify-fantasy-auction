using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Services;
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
    private readonly IBiddingService _biddingService;

    public AdminHubFunction(LeagifyAuctionDbContext context, ILogger<AdminHubFunction> logger, IBiddingService biddingService)
    {
        _context = context;
        _logger = logger;
        _biddingService = biddingService;
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
            Console.WriteLine($"DEBUG EXCEPTION: {ex}");
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
    /// Manually end current bidding and award school to high bidder (Auction Master only).
    /// Creates a DraftPick, deducts budget, clears bidding state, and advances the turn.
    /// </summary>
    [Function("EndCurrentBid")]
    public async Task<AdminActionResponse> EndCurrentBid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/end-bidding")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("End bidding request for auction {AuctionId}", auctionId);

            // Validate Auction Master authentication (or management token)
            var managementToken = GetManagementToken(req);
            User? admin = null;

            if (string.IsNullOrEmpty(managementToken))
            {
                admin = await ValidateAuctionMaster(req, auctionId);
                if (admin == null)
                {
                    var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                    return new AdminActionResponse { HttpResponse = unauthorizedResponse };
                }
            }

            // Complete the bidding using the service
            var result = await _biddingService.CompleteBiddingAsync(auctionId);

            if (!result.Success)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync(result.ErrorMessage ?? "Failed to complete bidding");
                return new AdminActionResponse { HttpResponse = errorResponse };
            }

            var endedBy = admin?.DisplayName ?? "Management";
            _logger.LogInformation("Auction Master {AdminName} manually ended bidding for auction {AuctionId}: {SchoolName} won by {Winner} for ${Amount}",
                endedBy, auctionId, result.SchoolName, result.WinnerDisplayName, result.WinningBid);

            var signalRMessages = new List<SignalRMessageAction>();

            // Broadcast school won event
            signalRMessages.Add(new SignalRMessageAction("BiddingCompleted")
            {
                GroupName = $"auction-{auctionId}",
                Arguments = new object[]
                {
                    new
                    {
                        DraftPickId = result.DraftPickId,
                        SchoolName = result.SchoolName,
                        WinningBid = result.WinningBid,
                        WinnerUserId = result.WinnerUserId,
                        WinnerDisplayName = result.WinnerDisplayName,
                        TeamId = result.TeamId,
                        TeamName = result.TeamName,
                        NextNominatorUserId = result.NextNominatorUserId,
                        NextNominatorDisplayName = result.NextNominatorDisplayName,
                        IsAuctionComplete = result.IsAuctionComplete,
                        EndedBy = endedBy,
                        EndedAt = DateTime.UtcNow,
                        Reason = "Manual end by Auction Master"
                    }
                }
            });

            // Broadcast turn change if auction continues
            if (!result.IsAuctionComplete && result.NextNominatorUserId != null)
            {
                signalRMessages.Add(new SignalRMessageAction("NominationTurnChanged")
                {
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            CurrentNominatorUserId = result.NextNominatorUserId,
                            CurrentNominatorDisplayName = result.NextNominatorDisplayName
                        }
                    }
                });
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "Bidding ended and school awarded",
                DraftPickId = result.DraftPickId,
                SchoolName = result.SchoolName,
                WinningBid = result.WinningBid,
                WinnerDisplayName = result.WinnerDisplayName,
                TeamName = result.TeamName,
                NextNominatorDisplayName = result.NextNominatorDisplayName,
                IsAuctionComplete = result.IsAuctionComplete
            });

            return new AdminActionResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages.ToArray()
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

    private static string? GetManagementToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Management-Token", out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
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
