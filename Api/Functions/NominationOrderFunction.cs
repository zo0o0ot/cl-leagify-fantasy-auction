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
/// Handles nomination order setup and turn management for auctions.
/// Manages the sequence of which teams nominate schools during the auction.
/// </summary>
public class NominationOrderFunction(LeagifyAuctionDbContext context, ILogger<NominationOrderFunction> logger)
{
    private readonly LeagifyAuctionDbContext _context = context;
    private readonly ILogger<NominationOrderFunction> _logger = logger;

    /// <summary>
    /// Gets the nomination order for an auction.
    /// Returns list of users in nomination order with their status.
    /// </summary>
    [Function("GetNominationOrder")]
    public async Task<HttpResponseData> GetNominationOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/nomination-order")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("GetNominationOrder called for auction {AuctionId}", auctionId);

        try
        {
            var nominationOrders = await _context.NominationOrders
                .Include(no => no.User)
                .Where(no => no.AuctionId == auctionId)
                .OrderBy(no => no.OrderPosition)
                .ToListAsync();

            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            var result = nominationOrders.Select(no => new
            {
                NominationOrderId = no.NominationOrderId,
                UserId = no.UserId,
                DisplayName = no.User.DisplayName,
                OrderPosition = no.OrderPosition,
                HasNominated = no.HasNominated,
                IsSkipped = no.IsSkipped,
                IsCurrentNominator = auction?.CurrentNominatorUserId == no.UserId
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nomination order for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, "Failed to get nomination order");
        }
    }

    /// <summary>
    /// Advances to the next nominator in the turn order.
    /// Called after a school is successfully won by a team.
    /// </summary>
    [Function("AdvanceNominationTurn")]
    public async Task<MultiResponse> AdvanceNominationTurn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/advance-turn")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("AdvanceNominationTurn called for auction {AuctionId}", auctionId);

        try
        {
            // Verify admin/auction master authentication
            var managementToken = GetManagementToken(req);
            if (string.IsNullOrEmpty(managementToken))
            {
                // Try session token for auction master
                var sessionToken = GetSessionToken(req);
                if (string.IsNullOrEmpty(sessionToken))
                {
                    return new MultiResponse
                    {
                        HttpResponse = await CreateUnauthorizedResponse(req)
                    };
                }

                // Verify user is auction master
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

                if (user == null || !user.UserRoles.Any(r => r.Role == "AuctionMaster"))
                {
                    return new MultiResponse
                    {
                        HttpResponse = await CreateUnauthorizedResponse(req)
                    };
                }
            }

            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Auction not found")
                };
            }

            // Get nomination orders
            var nominationOrders = await _context.NominationOrders
                .Include(no => no.User)
                .Where(no => no.AuctionId == auctionId)
                .OrderBy(no => no.OrderPosition)
                .ToListAsync();

            if (!nominationOrders.Any())
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "No nomination order configured for this auction")
                };
            }

            // Mark current nominator as having nominated
            if (auction.CurrentNominatorUserId != null)
            {
                var currentNominatorOrder = nominationOrders
                    .FirstOrDefault(no => no.UserId == auction.CurrentNominatorUserId);
                if (currentNominatorOrder != null)
                {
                    currentNominatorOrder.HasNominated = true;
                }
            }

            // Find next nominator (first one who hasn't nominated and isn't skipped)
            var nextNominator = nominationOrders
                .FirstOrDefault(no => !no.HasNominated && !no.IsSkipped);

            if (nextNominator == null)
            {
                // All users have nominated, reset for next round
                foreach (var no in nominationOrders)
                {
                    no.HasNominated = false;
                }

                nextNominator = nominationOrders.FirstOrDefault(no => !no.IsSkipped);
            }

            if (nextNominator != null)
            {
                auction.CurrentNominatorUserId = nextNominator.UserId;
                auction.ModifiedDate = DateTime.UtcNow;
            }
            else
            {
                // No valid nominators (all skipped?)
                auction.CurrentNominatorUserId = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Advanced nomination turn in auction {AuctionId} to user {UserId}",
                auctionId, auction.CurrentNominatorUserId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Turn advanced successfully",
                CurrentNominatorUserId = auction.CurrentNominatorUserId,
                CurrentNominatorDisplayName = nextNominator?.User.DisplayName
            });

            // Broadcast turn change
            var signalRMessages = new[]
            {
                new SignalRMessageAction
                {
                    Target = "NominationTurnChanged",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            CurrentNominatorUserId = auction.CurrentNominatorUserId,
                            CurrentNominatorDisplayName = nextNominator?.User.DisplayName
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
            _logger.LogError(ex, "Error advancing nomination turn in auction {AuctionId}", auctionId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to advance turn")
            };
        }
    }

    /// <summary>
    /// Completes bidding on current school and assigns it to the winning team.
    /// Advances to the next nominator after assignment.
    /// </summary>
    [Function("CompleteBidding")]
    public async Task<MultiResponse> CompleteBidding(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/complete-bidding")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("CompleteBidding called for auction {AuctionId}", auctionId);

        try
        {
            // Verify admin/auction master authentication
            var managementToken = GetManagementToken(req);
            var sessionToken = GetSessionToken(req);

            if (string.IsNullOrEmpty(managementToken) && string.IsNullOrEmpty(sessionToken))
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            // If using session token, verify user is auction master
            if (string.IsNullOrEmpty(managementToken))
            {
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

                if (user == null || !user.UserRoles.Any(r => r.Role == "AuctionMaster"))
                {
                    return new MultiResponse
                    {
                        HttpResponse = await CreateUnauthorizedResponse(req)
                    };
                }
            }

            var auction = await _context.Auctions
                .Include(a => a.CurrentSchool)
                    .ThenInclude(s => s != null ? s.School : null)
                .Include(a => a.CurrentHighBidderUser)
                    .ThenInclude(u => u != null ? u.UserRoles : null)
                        .ThenInclude(ur => ur.Team)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Auction not found")
                };
            }

            if (auction.CurrentSchoolId == null || auction.CurrentHighBidderUserId == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "No active bidding to complete")
                };
            }

            var winningUser = auction.CurrentHighBidderUser!;

            // Get winning user's team
            var winningTeam = winningUser.UserRoles
                .Where(ur => ur.TeamId != null && ur.Team != null)
                .OrderBy(ur => ur.Role == "TeamCoach" ? 0 : 1)
                .FirstOrDefault()?.Team;

            if (winningTeam == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Winning bidder must be assigned to a team")
                };
            }

            // Create draft pick record (initially without roster position assignment)
            var draftPick = new DraftPick
            {
                AuctionId = auctionId,
                TeamId = winningTeam.TeamId,
                AuctionSchoolId = auction.CurrentSchoolId.Value,
                RosterPositionId = 0, // Will be assigned later by user
                WinningBid = auction.CurrentHighBid!.Value,
                NominatedByUserId = auction.CurrentNominatorUserId ?? winningUser.UserId, // Fallback to winner if nominator not tracked
                WonByUserId = winningUser.UserId,
                PickOrder = await _context.DraftPicks.CountAsync(dp => dp.AuctionId == auctionId) + 1,
                DraftedDate = DateTime.UtcNow,
                IsAssignmentConfirmed = false
            };

            _context.DraftPicks.Add(draftPick);

            // Update team budget
            winningTeam.RemainingBudget -= auction.CurrentHighBid.Value;

            // Mark the winning bid in history
            var winningBidHistory = await _context.BidHistories
                .Where(bh => bh.AuctionId == auctionId &&
                            bh.AuctionSchoolId == auction.CurrentSchoolId &&
                            bh.UserId == winningUser.UserId &&
                            bh.BidAmount == auction.CurrentHighBid.Value)
                .OrderByDescending(bh => bh.BidDate)
                .FirstOrDefaultAsync();

            if (winningBidHistory != null)
            {
                winningBidHistory.IsWinningBid = true;
            }

            // Clear current bidding state
            var completedSchoolName = auction.CurrentSchool?.School?.Name;
            auction.CurrentSchoolId = null;
            auction.CurrentHighBid = null;
            auction.CurrentHighBidderUserId = null;
            auction.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Completed bidding for school {SchoolName} in auction {AuctionId}, won by user {UserId} for ${Amount}",
                completedSchoolName, auctionId, winningUser.UserId, draftPick.WinningBid);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Bidding completed successfully",
                DraftPickId = draftPick.DraftPickId,
                SchoolName = completedSchoolName,
                WinningBid = draftPick.WinningBid,
                WinnerDisplayName = winningUser.DisplayName,
                TeamName = winningTeam.TeamName
            });

            // Broadcast bidding completion
            var signalRMessages = new List<SignalRMessageAction>
            {
                new SignalRMessageAction
                {
                    Target = "BiddingCompleted",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            DraftPickId = draftPick.DraftPickId,
                            SchoolName = completedSchoolName,
                            WinningBid = draftPick.WinningBid,
                            WinnerUserId = winningUser.UserId,
                            WinnerDisplayName = winningUser.DisplayName,
                            TeamId = winningTeam.TeamId,
                            TeamName = winningTeam.TeamName
                        }
                    }
                }
            };

            return new MultiResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing bidding in auction {AuctionId}", auctionId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to complete bidding")
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

    private static string? GetManagementToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Management-Token", out var values))
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
