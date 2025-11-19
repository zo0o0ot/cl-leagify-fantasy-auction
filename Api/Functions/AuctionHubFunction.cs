using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using System.Net;
using System.Text.Json;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Function handling real-time auction operations including nominations and bidding.
/// Coordinates turn-based nominations with automatic bid placement and SignalR broadcasts.
/// </summary>
public class AuctionHubFunction(LeagifyAuctionDbContext context, ILogger<AuctionHubFunction> logger)
{
    private readonly LeagifyAuctionDbContext _context = context;
    private readonly ILogger<AuctionHubFunction> _logger = logger;

    /// <summary>
    /// Gets the current bidding state for an auction.
    /// Returns current school being bid on, high bid, and whose turn it is to nominate.
    /// </summary>
    [Function("GetBiddingState")]
    public async Task<HttpResponseData> GetBiddingState(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/bidding-state")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("GetBiddingState called for auction {AuctionId}", auctionId);

        try
        {
            // Verify user authentication
            var sessionToken = GetSessionToken(req);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

            if (user == null)
            {
                return await CreateUnauthorizedResponse(req);
            }

            // Get auction with current bidding state
            var auction = await _context.Auctions
                .Include(a => a.CurrentSchool)
                    .ThenInclude(s => s != null ? s.School : null)
                .Include(a => a.CurrentNominatorUser)
                .Include(a => a.CurrentHighBidderUser)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return await CreateNotFoundResponse(req, "Auction not found");
            }

            var biddingState = new
            {
                AuctionId = auction.AuctionId,
                Status = auction.Status,
                CurrentNominatorUserId = auction.CurrentNominatorUserId,
                CurrentNominatorDisplayName = auction.CurrentNominatorUser?.DisplayName,
                CurrentSchoolId = auction.CurrentSchoolId,
                CurrentSchoolName = auction.CurrentSchool?.School?.Name,
                CurrentHighBid = auction.CurrentHighBid,
                CurrentHighBidderUserId = auction.CurrentHighBidderUserId,
                CurrentHighBidderDisplayName = auction.CurrentHighBidderUser?.DisplayName,
                IsActiveBidding = auction.CurrentSchoolId != null
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(biddingState);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bidding state for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, "Failed to get bidding state");
        }
    }

    /// <summary>
    /// Nominates a school for bidding and automatically places a $1 bid for the nominating user.
    /// Broadcasts nomination event to all auction participants via SignalR.
    /// </summary>
    [Function("NominateSchool")]
    public async Task<MultiResponse> NominateSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/nominate")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("NominateSchool called for auction {AuctionId}", auctionId);

        try
        {
            // Verify user authentication
            var sessionToken = GetSessionToken(req);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            var user = await _context.Users
                .Include(u => u.Team)
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

            if (user == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var nominationRequest = JsonSerializer.Deserialize<NominationRequest>(requestBody ?? "{}");

            if (nominationRequest?.AuctionSchoolId == null || nominationRequest.AuctionSchoolId <= 0)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Valid auction school ID required")
                };
            }

            // Get auction and verify status
            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Auction not found")
                };
            }

            if (auction.Status != "InProgress")
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Auction must be in progress to nominate schools")
                };
            }

            // Verify it's the user's turn to nominate
            if (auction.CurrentNominatorUserId != null && auction.CurrentNominatorUserId != user.UserId)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "It is not your turn to nominate")
                };
            }

            // Verify there's no active bidding
            if (auction.CurrentSchoolId != null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Cannot nominate while bidding is in progress")
                };
            }

            // Get the school being nominated
            var auctionSchool = await _context.AuctionSchools
                .Include(s => s.School)
                .FirstOrDefaultAsync(s => s.AuctionSchoolId == nominationRequest.AuctionSchoolId && s.AuctionId == auctionId);

            if (auctionSchool == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "School not found in this auction")
                };
            }

            // Check if school has already been won
            var existingPick = await _context.DraftPicks
                .AnyAsync(dp => dp.AuctionSchoolId == auctionSchool.AuctionSchoolId);

            if (existingPick)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "This school has already been drafted")
                };
            }

            // Verify user has roster space
            if (user.Team == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "You must be assigned to a team to nominate schools")
                };
            }

            var rosterPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .ToListAsync();

            var totalRosterSlots = rosterPositions.Sum(rp => rp.SlotsPerTeam);

            var currentPicks = await _context.DraftPicks
                .CountAsync(dp => dp.TeamId == user.Team.TeamId);

            if (currentPicks >= totalRosterSlots)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Your team's roster is full")
                };
            }

            // Verify user has sufficient budget (needs at least $1 + (remaining slots - 1))
            var remainingSlots = totalRosterSlots - currentPicks;
            var minimumBudget = 1 + (remainingSlots - 1); // $1 for this nomination + $1 per remaining slot

            if (user.Team.CurrentBudget < minimumBudget)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, $"Insufficient budget. You need at least ${minimumBudget} to nominate")
                };
            }

            // Update auction state with nominated school and automatic $1 bid
            auction.CurrentSchoolId = auctionSchool.AuctionSchoolId;
            auction.CurrentHighBid = 1.00m; // Auto-bid $1
            auction.CurrentHighBidderUserId = user.UserId;
            auction.ModifiedDate = DateTime.UtcNow;

            // Record the nomination in bid history
            var bidHistory = new BidHistory
            {
                AuctionId = auctionId,
                AuctionSchoolId = auctionSchool.AuctionSchoolId,
                UserId = user.UserId,
                BidAmount = 1.00m,
                BidType = "Nomination",
                BidDate = DateTime.UtcNow,
                Notes = $"Nominated {auctionSchool.School.Name}"
            };

            _context.BidHistories.Add(bidHistory);

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} nominated school {SchoolId} in auction {AuctionId}",
                user.UserId, auctionSchool.AuctionSchoolId, auctionId);

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "School nominated successfully",
                AuctionSchoolId = auctionSchool.AuctionSchoolId,
                SchoolName = auctionSchool.School.Name,
                CurrentHighBid = 1.00m,
                CurrentHighBidderUserId = user.UserId
            });

            // Create SignalR messages to broadcast to all auction participants
            var signalRMessages = new[]
            {
                new SignalRMessageAction
                {
                    Target = "BiddingStarted",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            AuctionSchoolId = auctionSchool.AuctionSchoolId,
                            SchoolName = auctionSchool.School.Name,
                            NominatedByUserId = user.UserId,
                            NominatedByDisplayName = user.DisplayName,
                            CurrentHighBid = 1.00m,
                            CurrentHighBidderUserId = user.UserId,
                            CurrentHighBidderDisplayName = user.DisplayName,
                            Conference = auctionSchool.Conference,
                            ProjectedPoints = auctionSchool.ProjectedPoints
                        }
                    }
                }
            };

            return new MultiResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error nominating school in auction {AuctionId}", auctionId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to nominate school")
            };
        }
    }

    /// <summary>
    /// Places a bid on the currently nominated school.
    /// Validates bid amount against budget and minimum bid increment.
    /// </summary>
    [Function("PlaceBid")]
    public async Task<MultiResponse> PlaceBid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/bid")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("PlaceBid called for auction {AuctionId}", auctionId);

        try
        {
            // Verify user authentication
            var sessionToken = GetSessionToken(req);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            var user = await _context.Users
                .Include(u => u.Team)
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

            if (user == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var bidRequest = JsonSerializer.Deserialize<BidRequest>(requestBody ?? "{}");

            if (bidRequest?.BidAmount == null || bidRequest.BidAmount <= 0)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Valid bid amount required")
                };
            }

            // Get auction with current bidding state
            var auction = await _context.Auctions
                .Include(a => a.CurrentSchool)
                    .ThenInclude(s => s != null ? s.School : null)
                .Include(a => a.CurrentHighBidderUser)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Auction not found")
                };
            }

            if (auction.Status != "InProgress")
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Auction must be in progress to place bids")
                };
            }

            // Verify there's active bidding
            if (auction.CurrentSchoolId == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "No school is currently being bid on")
                };
            }

            // Verify user has a team
            if (user.Team == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "You must be assigned to a team to place bids")
                };
            }

            // Verify bid is higher than current high bid
            if (bidRequest.BidAmount <= auction.CurrentHighBid)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, $"Bid must be higher than current high bid of ${auction.CurrentHighBid}")
                };
            }

            // Verify user has sufficient budget
            var rosterPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .ToListAsync();

            var totalRosterSlots = rosterPositions.Sum(rp => rp.SlotsPerTeam);

            var currentPicks = await _context.DraftPicks
                .CountAsync(dp => dp.TeamId == user.Team.TeamId);

            var remainingSlots = totalRosterSlots - currentPicks;
            var minimumBudget = bidRequest.BidAmount + (remainingSlots - 1); // Bid amount + $1 per remaining slot

            if (user.Team.CurrentBudget < minimumBudget)
            {
                var maxBid = user.Team.CurrentBudget - (remainingSlots - 1);
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, $"Insufficient budget. Your maximum bid is ${maxBid}")
                };
            }

            // Update auction with new high bid
            auction.CurrentHighBid = bidRequest.BidAmount;
            auction.CurrentHighBidderUserId = user.UserId;
            auction.ModifiedDate = DateTime.UtcNow;

            // Record the bid in history
            var bidHistory = new BidHistory
            {
                AuctionId = auctionId,
                AuctionSchoolId = auction.CurrentSchoolId,
                UserId = user.UserId,
                BidAmount = bidRequest.BidAmount,
                BidType = "Bid",
                BidDate = DateTime.UtcNow
            };

            _context.BidHistories.Add(bidHistory);

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} bid ${Amount} on school {SchoolId} in auction {AuctionId}",
                user.UserId, bidRequest.BidAmount, auction.CurrentSchoolId, auctionId);

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Bid placed successfully",
                BidAmount = bidRequest.BidAmount,
                CurrentHighBidderUserId = user.UserId,
                SchoolName = auction.CurrentSchool?.School?.Name
            });

            // Broadcast bid update to all participants
            var signalRMessages = new[]
            {
                new SignalRMessageAction
                {
                    Target = "BidPlaced",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            AuctionSchoolId = auction.CurrentSchoolId,
                            SchoolName = auction.CurrentSchool?.School?.Name,
                            BidAmount = bidRequest.BidAmount,
                            BidderUserId = user.UserId,
                            BidderDisplayName = user.DisplayName,
                            CurrentHighBid = bidRequest.BidAmount,
                            CurrentHighBidderUserId = user.UserId,
                            CurrentHighBidderDisplayName = user.DisplayName
                        }
                    }
                }
            };

            return new MultiResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing bid in auction {AuctionId}", auctionId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to place bid")
            };
        }
    }

    /// <summary>
    /// Allows a user to pass on bidding for the current school.
    /// Records pass in bid history for audit purposes.
    /// </summary>
    [Function("PassOnSchool")]
    public async Task<MultiResponse> PassOnSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/pass")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("PassOnSchool called for auction {AuctionId}", auctionId);

        try
        {
            // Verify user authentication
            var sessionToken = GetSessionToken(req);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

            if (user == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            // Get auction
            var auction = await _context.Auctions
                .Include(a => a.CurrentSchool)
                    .ThenInclude(s => s != null ? s.School : null)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Auction not found")
                };
            }

            // Verify there's active bidding
            if (auction.CurrentSchoolId == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "No school is currently being bid on")
                };
            }

            // Record the pass in bid history
            var bidHistory = new BidHistory
            {
                AuctionId = auctionId,
                AuctionSchoolId = auction.CurrentSchoolId,
                UserId = user.UserId,
                BidAmount = auction.CurrentHighBid ?? 0,
                BidType = "Pass",
                BidDate = DateTime.UtcNow
            };

            _context.BidHistories.Add(bidHistory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} passed on school {SchoolId} in auction {AuctionId}",
                user.UserId, auction.CurrentSchoolId, auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Pass recorded",
                SchoolName = auction.CurrentSchool?.School?.Name
            });

            // Broadcast pass event
            var signalRMessages = new[]
            {
                new SignalRMessageAction
                {
                    Target = "UserPassed",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            UserId = user.UserId,
                            DisplayName = user.DisplayName,
                            SchoolName = auction.CurrentSchool?.School?.Name
                        }
                    }
                }
            };

            return new MultiResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording pass in auction {AuctionId}", auctionId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to record pass")
            };
        }
    }

    // Helper methods

    private static string? GetSessionToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Auction-Token", out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
    }

    private static async Task<HttpResponseData> CreateUnauthorizedResponse(HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        await response.WriteStringAsync("Unauthorized");
        return response;
    }

    private static async Task<HttpResponseData> CreateNotFoundResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.NotFound);
        await response.WriteStringAsync(message);
        return response;
    }

    private static async Task<HttpResponseData> CreateBadRequestResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteStringAsync(message);
        return response;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteStringAsync(message);
        return response;
    }

    // DTOs

    private class NominationRequest
    {
        public int AuctionSchoolId { get; set; }
    }

    private class BidRequest
    {
        public decimal BidAmount { get; set; }
    }

    // SignalR support classes

    public class MultiResponse
    {
        public HttpResponseData? HttpResponse { get; set; }
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class SignalRMessageAction
    {
        public string Target { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public object[] Arguments { get; set; } = Array.Empty<object>();
    }
}
