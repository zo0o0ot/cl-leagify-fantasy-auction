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
/// Azure Functions for waiting room functionality including test bidding and readiness tracking.
/// </summary>
public class WaitingRoomFunction
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<WaitingRoomFunction> _logger;

    // Virtual test schools (don't exist in database)
    private static readonly Dictionary<int, string> TEST_SCHOOLS = new()
    {
        { -1, "Vermont A&M" },
        { -2, "Luther College" },
        { -3, "Oxford University" },
        { -4, "University of Northern Iowa" },
        { -5, "DeVry University" }
    };

    public WaitingRoomFunction(LeagifyAuctionDbContext context, ILogger<WaitingRoomFunction> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all waiting room data including auction info, user readiness, test school, and schools list.
    /// </summary>
    [Function("GetWaitingRoomData")]
    public async Task<HttpResponseData> GetWaitingRoomData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/waiting-room")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Getting waiting room data for auction {AuctionId}", auctionId);

            // Validate session token
            var user = await ValidateSessionToken(req, auctionId);
            if (user == null)
            {
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // Get auction data
            var auction = await _context.Auctions
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            // Get current test school ID from auction
            var currentTestSchoolId = auction.CurrentTestSchoolId;
            var currentTestSchoolNotes = $"TestSchool:{currentTestSchoolId}";

            // Get current test bid info for the current test school only
            var currentTestBid = await _context.BidHistories
                .Include(b => b.User)
                .Where(b => b.AuctionId == auctionId &&
                           b.BidType == "TestBid" &&
                           b.Notes == currentTestSchoolNotes &&
                           !b.IsWinningBid)
                .OrderByDescending(b => b.BidDate)
                .FirstOrDefaultAsync();

            // Get test bid history for current school only
            var testBidHistory = await _context.BidHistories
                .Include(b => b.User)
                .Where(b => b.AuctionId == auctionId &&
                           b.BidType == "TestBid" &&
                           b.Notes == currentTestSchoolNotes)
                .OrderByDescending(b => b.BidDate)
                .Take(10)
                .Select(b => new
                {
                    BidderName = b.User.DisplayName,
                    Amount = b.BidAmount,
                    BidDate = b.BidDate
                })
                .ToListAsync();

            // Get all schools for preview - show schools for this auction with conference data
            // These are the available schools that users can bid on in this specific auction
            var schools = await _context.AuctionSchools
                .Include(a => a.School)
                .Where(a => a.AuctionId == auctionId && !a.IsTestSchool)
                .AsNoTracking()
                .OrderBy(a => a.School.Name)
                .Select(a => new
                {
                    Name = a.School.Name,
                    Conference = a.Conference
                })
                .ToListAsync();

            // Get nomination order
            var nominationOrder = await _context.NominationOrders
                .Include(n => n.User)
                .Where(n => n.AuctionId == auctionId)
                .OrderBy(n => n.OrderPosition)
                .Select(n => n.User.DisplayName)
                .ToListAsync();

            // If no nomination order exists, try to get from Teams
            if (!nominationOrder.Any())
            {
                nominationOrder = await _context.Teams
                    .Include(t => t.User)
                    .Where(t => t.AuctionId == auctionId && t.IsActive)
                    .OrderBy(t => t.NominationOrder)
                    .Select(t => t.User.DisplayName)
                    .ToListAsync();
            }

            // Get all participants with their bidding status
            var participants = await _context.Users
                .Where(u => u.AuctionId == auctionId)
                .OrderBy(u => u.DisplayName)
                .Select(u => new
                {
                    DisplayName = u.DisplayName,
                    HasTestedBidding = u.HasTestedBidding,
                    IsReadyToDraft = u.IsReadyToDraft,
                    HasPassedOnTestBid = u.HasPassedOnTestBid,
                    IsConnected = u.IsConnected
                })
                .ToListAsync();

            // Get user's winning test bids
            var userWinningTestBids = await _context.BidHistories
                .Where(b => b.AuctionId == auctionId &&
                           b.UserId == user.UserId &&
                           b.BidType == "TestBid" &&
                           b.IsWinningBid == true &&
                           b.Notes != null && b.Notes.StartsWith("TestSchool:"))
                .OrderByDescending(b => b.BidDate)
                .Select(b => new
                {
                    // Extract school ID from Notes field (format: "TestSchool:-1")
                    SchoolId = int.Parse(b.Notes!.Substring("TestSchool:".Length)),
                    Amount = b.BidAmount,
                    WonDate = b.BidDate
                })
                .ToListAsync();

            // Map school IDs to names for winning bids
            var userWins = userWinningTestBids.Select(w => new
            {
                SchoolId = w.SchoolId,
                SchoolName = TEST_SCHOOLS.ContainsKey(w.SchoolId) ? TEST_SCHOOLS[w.SchoolId] : "Unknown School",
                Amount = w.Amount,
                WonDate = w.WonDate
            }).ToList();

            // Build test schools list with their current bidding status
            var testSchools = TEST_SCHOOLS.Select(kvp => new
            {
                SchoolId = kvp.Key,
                Name = kvp.Value,
                CurrentBid = currentTestBid?.BidAmount ?? 0,
                CurrentBidderName = currentTestBid?.User.DisplayName ?? string.Empty
            }).ToList();

            // Build response
            var response = new
            {
                Auction = new
                {
                    AuctionName = auction.Name,
                    Status = auction.Status
                },
                User = new
                {
                    DisplayName = user.DisplayName,
                    HasTestedBidding = user.HasTestedBidding,
                    IsReadyToDraft = user.IsReadyToDraft,
                    HasPassedOnTestBid = user.HasPassedOnTestBid
                },
                TestSchool = new
                {
                    SchoolId = currentTestSchoolId,
                    Name = TEST_SCHOOLS[currentTestSchoolId],
                    CurrentBid = currentTestBid?.BidAmount ?? 0,
                    CurrentBidderName = currentTestBid?.User.DisplayName ?? string.Empty
                },
                TestSchools = testSchools,
                TestBidHistory = testBidHistory,
                Schools = schools,
                NominationOrder = nominationOrder,
                Participants = participants,
                UserWonTestSchools = userWins
            };

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(response);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting waiting room data for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Places a test bid on Vermont A&M during the waiting room phase.
    /// Broadcasts the bid to all participants in the waiting room via SignalR.
    /// </summary>
    [Function("PlaceTestBid")]
    public async Task<TestBidResponse> PlaceTestBid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/test-bid")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Placing test bid for auction {AuctionId}", auctionId);

            // Validate session token
            var user = await ValidateSessionToken(req, auctionId);
            if (user == null)
            {
                _logger.LogWarning("Unauthorized test bid attempt for auction {AuctionId}", auctionId);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return new TestBidResponse { HttpResponse = unauthorizedResponse };
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            _logger.LogInformation("Test bid request body: {RequestBody}", requestBody);

            var bidRequest = JsonSerializer.Deserialize<TestBidRequest>(requestBody ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (bidRequest == null)
            {
                _logger.LogError("Failed to deserialize test bid request for auction {AuctionId}", auctionId);
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Failed to parse request body");
                return new TestBidResponse { HttpResponse = badRequest };
            }

            if (bidRequest.Amount <= 0)
            {
                _logger.LogWarning("Invalid bid amount for auction {AuctionId}: {Amount}", auctionId, bidRequest.Amount);
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync($"Invalid bid amount: {bidRequest.Amount}. Must be greater than 0.");
                return new TestBidResponse { HttpResponse = badRequest };
            }

            _logger.LogInformation("Processing test bid: Auction={AuctionId}, User={UserId}, Amount={Amount}",
                auctionId, user.UserId, bidRequest.Amount);

            // Get current auction to determine which test school is active
            var auction = await _context.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Auction not found");
                return new TestBidResponse { HttpResponse = notFound };
            }

            var currentTestSchoolId = auction.CurrentTestSchoolId;
            var currentTestSchoolNotes = $"TestSchool:{currentTestSchoolId}";

            // Validate bid amount (must be higher than current bid for this specific test school)
            var currentHighBid = await _context.BidHistories
                .Where(b => b.AuctionId == auctionId &&
                           b.BidType == "TestBid" &&
                           b.Notes == currentTestSchoolNotes &&
                           !b.IsWinningBid)
                .OrderByDescending(b => b.BidDate)
                .Select(b => b.BidAmount)
                .FirstOrDefaultAsync();

            if (bidRequest.Amount <= currentHighBid)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync($"Bid must be higher than current bid of ${currentHighBid}");
                return new TestBidResponse { HttpResponse = conflict };
            }

            // Validate user hasn't passed (permanent pass - cannot bid again)
            if (user.HasPassedOnTestBid)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync("You have already passed on this school and cannot bid again");
                return new TestBidResponse { HttpResponse = conflict };
            }

            // Create test bid record (using virtual school - no AuctionSchoolId)
            // Test bids have null AuctionSchoolId; test school ID is stored in Notes
            var testBid = new BidHistory
            {
                AuctionId = auctionId,
                AuctionSchoolId = null, // Virtual test school - no real school ID
                UserId = user.UserId,
                BidAmount = bidRequest.Amount,
                BidType = "TestBid",
                BidDate = DateTime.UtcNow,
                IsWinningBid = false,
                Notes = currentTestSchoolNotes // e.g., "TestSchool:-1" for Vermont A&M
            };

            _context.BidHistories.Add(testBid);

            // Update user's HasTestedBidding flag
            var isFirstTestBid = !user.HasTestedBidding;
            if (isFirstTestBid)
            {
                user.HasTestedBidding = true;
            }

            // NOTE: Passing is PERMANENT - users who pass cannot bid again on this school
            // This matches regular auction behavior where passing is a primary win condition

            await _context.SaveChangesAsync();

            _logger.LogInformation("Test bid placed: User {UserId} bid ${Amount} on test school", user.UserId, bidRequest.Amount);

            // Build SignalR messages list - broadcast to all (clients filter by auction ID)
            var signalRMessages = new List<SignalRMessageAction>
            {
                new SignalRMessageAction("TestBidPlaced")
                {
                    // No GroupName - broadcast to all connections
                    Arguments = new object[] { auctionId, user.DisplayName, bidRequest.Amount, testBid.BidDate }
                }
            };

            // If this is user's first test bid, broadcast that too
            if (isFirstTestBid)
            {
                signalRMessages.Add(new SignalRMessageAction("UserTestedBidding")
                {
                    // No GroupName - broadcast to all connections
                    Arguments = new object[] { auctionId, user.DisplayName }
                });
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                BidAmount = bidRequest.Amount,
                BidderName = user.DisplayName,
                HasTestedBidding = true
            });

            return new TestBidResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing test bid for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new TestBidResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Updates user's readiness status (IsReadyToDraft flag).
    /// Broadcasts the status change to all participants in the waiting room via SignalR.
    /// </summary>
    [Function("UpdateReadyStatus")]
    public async Task<ReadyStatusResponse> UpdateReadyStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/ready-status")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Updating ready status for auction {AuctionId}", auctionId);

            // Validate session token
            var user = await ValidateSessionToken(req, auctionId);
            if (user == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return new ReadyStatusResponse { HttpResponse = unauthorizedResponse };
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            _logger.LogInformation("📥 Request body: {RequestBody}", requestBody);

            var readyRequest = JsonSerializer.Deserialize<ReadyStatusRequest>(requestBody ?? "{}", new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (readyRequest == null)
            {
                _logger.LogWarning("❌ Failed to deserialize ReadyStatusRequest");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request");
                return new ReadyStatusResponse { HttpResponse = badRequest };
            }

            _logger.LogInformation("🔄 Parsed IsReady value: {IsReady}", readyRequest.IsReady);
            _logger.LogInformation("📊 User {DisplayName} current status: {CurrentStatus}, new status: {NewStatus}",
                user.DisplayName,
                user.IsReadyToDraft ? "ready" : "not ready",
                readyRequest.IsReady ? "ready" : "not ready");

            // Update readiness status
            user.IsReadyToDraft = readyRequest.IsReady;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Ready status updated: User {UserId} ({DisplayName}) is now {Status}",
                user.UserId,
                user.DisplayName,
                readyRequest.IsReady ? "ready" : "not ready");

            // Create SignalR message - broadcast to all (clients filter by auction ID)
            var signalRMessage = new SignalRMessageAction("ReadinessUpdated")
            {
                // No GroupName - broadcast to all connections
                Arguments = new object[] { auctionId, user.DisplayName, readyRequest.IsReady }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                DisplayName = user.DisplayName,
                IsReadyToDraft = user.IsReadyToDraft
            });

            return new ReadyStatusResponse
            {
                HttpResponse = response,
                SignalRMessages = new[] { signalRMessage }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ready status for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new ReadyStatusResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Marks the user as having passed on the current test bid.
    /// Broadcasts the pass status to all participants in the waiting room via SignalR.
    /// </summary>
    [Function("PassOnTestBid")]
    public async Task<PassResponse> PassOnTestBid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/test-bid/pass")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("User passing on test bid for auction {AuctionId}", auctionId);

            // Validate session token
            var user = await ValidateSessionToken(req, auctionId);
            if (user == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return new PassResponse { HttpResponse = unauthorizedResponse };
            }

            // Update user's pass status
            user.HasPassedOnTestBid = true;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Pass status updated: User {UserId} ({DisplayName}) passed on test bid",
                user.UserId, user.DisplayName);

            // Create SignalR message - broadcast to all (clients filter by auction ID)
            var signalRMessage = new SignalRMessageAction("UserPassedOnTestBid")
            {
                // No GroupName - broadcast to all connections
                Arguments = new object[] { auctionId, user.DisplayName }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                DisplayName = user.DisplayName,
                HasPassedOnTestBid = true
            });

            return new PassResponse
            {
                HttpResponse = response,
                SignalRMessages = new[] { signalRMessage }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pass on test bid for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new PassResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Completes the current test bidding round and advances to the next test school.
    /// Admin-only endpoint for waiting room management.
    /// </summary>
    [Function("CompleteTestBidding")]
    public async Task<CompleteTestBiddingResponse> CompleteTestBidding(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/test-bid/complete")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Completing test bidding for auction {AuctionId}", auctionId);

            // Get auction to access current test school
            var auction = await _context.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Auction not found");
                return new CompleteTestBiddingResponse { HttpResponse = notFound };
            }

            var currentTestSchoolId = auction.CurrentTestSchoolId;
            var currentTestSchoolName = TEST_SCHOOLS[currentTestSchoolId];
            var currentTestSchoolNotes = $"TestSchool:{currentTestSchoolId}";

            // Get the current high bid for this specific test school
            var currentBid = await _context.BidHistories
                .Include(b => b.User)
                .Where(b => b.AuctionId == auctionId &&
                           b.BidType == "TestBid" &&
                           b.Notes == currentTestSchoolNotes &&
                           !b.IsWinningBid)
                .OrderByDescending(b => b.BidDate)
                .FirstOrDefaultAsync();

            string? winnerName = null;
            decimal? winningBid = null;

            if (currentBid != null)
            {
                // Mark the current high bid as winning
                currentBid.IsWinningBid = true;
                winnerName = currentBid.User.DisplayName;
                winningBid = currentBid.BidAmount;
                _logger.LogInformation("Marked test bid {BidId} as winning: {Winner} won {School} for ${Amount}",
                    currentBid.BidHistoryId, winnerName, currentTestSchoolName, winningBid);
            }

            // Advance to next test school (cycle through -1 to -5)
            var nextSchoolId = currentTestSchoolId - 1;
            if (nextSchoolId < -5) nextSchoolId = -1; // Wrap back to Vermont A&M
            auction.CurrentTestSchoolId = nextSchoolId;

            _logger.LogInformation("Advancing from {CurrentSchool} (ID:{CurrentId}) to {NextSchool} (ID:{NextId})",
                currentTestSchoolName, currentTestSchoolId, TEST_SCHOOLS[nextSchoolId], nextSchoolId);

            // Reset all users' pass flags for the next round
            var users = await _context.Users
                .Where(u => u.AuctionId == auctionId && u.HasPassedOnTestBid)
                .ToListAsync();

            foreach (var user in users)
            {
                user.HasPassedOnTestBid = false;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Test bidding completed for auction {AuctionId}. Reset {UserCount} user pass flags",
                auctionId, users.Count);

            // Broadcast completion via SignalR with details
            var signalRMessages = new List<SignalRMessageAction>
            {
                new SignalRMessageAction("TestBiddingCompleted")
                {
                    Arguments = new object[] {
                        auctionId,
                        currentTestSchoolName,
                        winnerName ?? "No bids",
                        winningBid ?? 0m,
                        TEST_SCHOOLS[nextSchoolId] // Next school name
                    }
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new {
                Success = true,
                Message = $"Completed bidding on {currentTestSchoolName}",
                Winner = winnerName,
                WinningBid = winningBid,
                NextSchool = TEST_SCHOOLS[nextSchoolId]
            });

            return new CompleteTestBiddingResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing test bidding for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new CompleteTestBiddingResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Resets all test bids and user readiness flags for the auction.
    /// Admin-only endpoint for waiting room management.
    /// </summary>
    [Function("ResetTestBids")]
    public async Task<ResetTestBidsResponse> ResetTestBids(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/test-bid/reset")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Resetting test bids for auction {AuctionId}", auctionId);

            // Delete all test bids
            var testBids = await _context.BidHistories
                .Where(b => b.AuctionId == auctionId && b.BidType == "TestBid")
                .ToListAsync();

            _context.BidHistories.RemoveRange(testBids);

            // Reset all user test bidding flags
            var users = await _context.Users
                .Where(u => u.AuctionId == auctionId)
                .ToListAsync();

            foreach (var user in users)
            {
                user.HasTestedBidding = false;
                user.HasPassedOnTestBid = false;
                user.IsReadyToDraft = false;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Test bids reset for auction {AuctionId}. Deleted {Count} bids and reset {UserCount} users",
                auctionId, testBids.Count, users.Count);

            // Broadcast reset via SignalR
            var signalRMessages = new List<SignalRMessageAction>
            {
                new SignalRMessageAction("TestBidsReset")
                {
                    Arguments = new object[] { auctionId }
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "All test bids and readiness flags have been reset",
                BidsDeleted = testBids.Count,
                UsersReset = users.Count
            });

            return new ResetTestBidsResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting test bids for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return new ResetTestBidsResponse { HttpResponse = errorResponse };
        }
    }

    /// <summary>
    /// Gets all available test schools with their current bidding status.
    /// Admin-only endpoint for waiting room management.
    /// </summary>
    [Function("GetTestSchools")]
    public async Task<HttpResponseData> GetTestSchools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/test-schools")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Getting test schools for auction {AuctionId}", auctionId);

            // Get all test bids grouped by virtual school ID (stored in Notes field)
            var testBids = await _context.BidHistories
                .Include(b => b.User)
                .Where(b => b.AuctionId == auctionId && b.BidType == "TestBid")
                .OrderByDescending(b => b.BidDate)
                .ToListAsync();

            // Build test school status
            var testSchools = TEST_SCHOOLS.Select(kvp => new
            {
                SchoolId = kvp.Key,
                SchoolName = kvp.Value,
                CurrentBid = testBids
                    .Where(b => !b.IsWinningBid)
                    .OrderByDescending(b => b.BidDate)
                    .FirstOrDefault()?.BidAmount ?? 0m,
                CurrentBidderName = testBids
                    .Where(b => !b.IsWinningBid)
                    .OrderByDescending(b => b.BidDate)
                    .FirstOrDefault()?.User.DisplayName ?? string.Empty,
                TotalBids = testBids.Count(b => !b.IsWinningBid),
                IsCompleted = testBids.Any(b => b.IsWinningBid)
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(testSchools);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting test schools for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Validates the session token from the request header and returns the authenticated user.
    /// </summary>
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

    // Request DTOs
    private class TestBidRequest
    {
        public decimal Amount { get; set; }
    }

    private class ReadyStatusRequest
    {
        public bool IsReady { get; set; }
    }

    // Response classes for multi-output functions (HTTP + SignalR)
    public class TestBidResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class ReadyStatusResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class PassResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class CompleteTestBiddingResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class ResetTestBidsResponse
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [SignalROutput(HubName = "auctionhub")]
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }
}
