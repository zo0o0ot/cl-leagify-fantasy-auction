using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Functions for waiting room functionality including test bidding and readiness tracking.
/// </summary>
public class WaitingRoomFunction
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<WaitingRoomFunction> _logger;

    // Virtual test school ID (doesn't exist in database)
    private const int VIRTUAL_TEST_SCHOOL_ID = -1;
    private const string TEST_SCHOOL_NAME = "Vermont A&M";

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

            // Get current test bid info for virtual test school
            var currentTestBid = await _context.BidHistories
                .Include(b => b.User)
                .Where(b => b.AuctionId == auctionId && b.BidType == "TestBid")
                .OrderByDescending(b => b.BidDate)
                .FirstOrDefaultAsync();

            // Get test bid history
            var testBidHistory = await _context.BidHistories
                .Include(b => b.User)
                .Where(b => b.AuctionId == auctionId && b.BidType == "TestBid")
                .OrderByDescending(b => b.BidDate)
                .Take(10)
                .Select(b => new
                {
                    BidderName = b.User.DisplayName,
                    Amount = b.BidAmount,
                    BidDate = b.BidDate
                })
                .ToListAsync();

            // Get all schools for preview (all real schools in auction)
            var schools = await _context.AuctionSchools
                .Include(s => s.School)
                .Where(s => s.AuctionId == auctionId)
                .OrderBy(s => s.ImportOrder)
                .Select(s => new
                {
                    Name = s.School.Name,
                    Conference = s.Conference
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
                    IsReadyToDraft = user.IsReadyToDraft
                },
                TestSchool = new
                {
                    SchoolId = VIRTUAL_TEST_SCHOOL_ID,
                    Name = TEST_SCHOOL_NAME,
                    CurrentBid = currentTestBid?.BidAmount ?? 0,
                    CurrentBidderName = currentTestBid?.User.DisplayName ?? string.Empty
                },
                TestBidHistory = testBidHistory,
                Schools = schools,
                NominationOrder = nominationOrder
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
    /// </summary>
    [Function("PlaceTestBid")]
    public async Task<HttpResponseData> PlaceTestBid(
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
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var bidRequest = JsonSerializer.Deserialize<TestBidRequest>(requestBody ?? "{}");

            if (bidRequest == null || bidRequest.Amount <= 0)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid bid amount");
                return badRequest;
            }

            // Validate bid amount (must be higher than current bid)
            var currentHighBid = await _context.BidHistories
                .Where(b => b.AuctionId == auctionId && b.BidType == "TestBid")
                .OrderByDescending(b => b.BidDate)
                .Select(b => b.BidAmount)
                .FirstOrDefaultAsync();

            if (bidRequest.Amount <= currentHighBid)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteStringAsync($"Bid must be higher than current bid of ${currentHighBid}");
                return conflict;
            }

            // Create test bid record (using virtual school - no AuctionSchoolId)
            // Test bids have null AuctionSchoolId since Vermont A&M is virtual
            var testBid = new BidHistory
            {
                AuctionId = auctionId,
                AuctionSchoolId = null, // Virtual test school - no real school ID
                UserId = user.UserId,
                BidAmount = bidRequest.Amount,
                BidType = "TestBid",
                BidDate = DateTime.UtcNow,
                IsWinningBid = false,
                Notes = $"Waiting room test bid on {TEST_SCHOOL_NAME}"
            };

            _context.BidHistories.Add(testBid);

            // Update user's HasTestedBidding flag
            if (!user.HasTestedBidding)
            {
                user.HasTestedBidding = true;
                _context.Users.Update(user);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Test bid placed: User {UserId} bid ${Amount} on test school", user.UserId, bidRequest.Amount);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                BidAmount = bidRequest.Amount,
                HasTestedBidding = true
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing test bid for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Updates user's readiness status (IsReadyToDraft flag).
    /// </summary>
    [Function("UpdateReadyStatus")]
    public async Task<HttpResponseData> UpdateReadyStatus(
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
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }

            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var readyRequest = JsonSerializer.Deserialize<ReadyStatusRequest>(requestBody ?? "{}");

            if (readyRequest == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request");
                return badRequest;
            }

            // Update readiness status
            user.IsReadyToDraft = readyRequest.IsReady;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ready status updated: User {UserId} is {Status}",
                user.UserId,
                readyRequest.IsReady ? "ready" : "not ready");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                IsReadyToDraft = user.IsReadyToDraft
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ready status for auction {AuctionId}", auctionId);
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
}
