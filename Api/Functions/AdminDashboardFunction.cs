using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;

namespace LeagifyFantasyAuction.Api.Functions;

public class AdminDashboardFunction
{
    private readonly ILogger<AdminDashboardFunction> _logger;
    private readonly LeagifyAuctionDbContext _dbContext;

    public AdminDashboardFunction(ILogger<AdminDashboardFunction> logger, LeagifyAuctionDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    [Function("GetAuctions")]
    public async Task<HttpResponseData> GetAuctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "management/auctions")] HttpRequestData req)
    {
        _logger.LogInformation("Admin get auctions request received");

        // Validate admin token
        if (!IsValidAdminRequest(req))
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            // TODO: Replace with actual database query
            var mockAuctions = new List<AuctionSummary>
            {
                new AuctionSummary
                {
                    AuctionId = 1,
                    Name = "Test Auction 1",
                    JoinCode = "ABC123",
                    Status = "Draft",
                    ParticipantCount = 6,
                    CreatedDate = DateTime.UtcNow.AddDays(-2),
                    LastActivity = DateTime.UtcNow.AddMinutes(-30)
                },
                new AuctionSummary
                {
                    AuctionId = 2,
                    Name = "Live Auction Demo",
                    JoinCode = "XYZ789",
                    Status = "InProgress",
                    ParticipantCount = 8,
                    CreatedDate = DateTime.UtcNow.AddDays(-1),
                    LastActivity = DateTime.UtcNow.AddMinutes(-5)
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(mockAuctions));
            response.Headers.Add("Content-Type", "application/json");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auctions for admin dashboard");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to retrieve auctions");
            return errorResponse;
        }
    }

    [Function("DeleteAuction")]
    public async Task<HttpResponseData> DeleteAuction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "management/auctions/{auctionId:int}")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation($"Admin delete auction {auctionId} request received");

        // Validate admin token
        if (!IsValidAdminRequest(req))
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            // Find the auction to delete
            var auction = await _dbContext.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                _logger.LogWarning($"Auction {auctionId} not found for deletion");
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { success = false, message = $"Auction {auctionId} not found" }));
                notFoundResponse.Headers.Add("Content-Type", "application/json");
                return notFoundResponse;
            }

            // Delete the auction
            _dbContext.Auctions.Remove(auction);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Successfully deleted auction {auctionId} ({auction.Name})");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, message = $"Auction {auctionId} deleted successfully" }));
            response.Headers.Add("Content-Type", "application/json");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting auction {auctionId}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to delete auction");
            return errorResponse;
        }
    }

    [Function("ArchiveCompletedAuctions")]
    public async Task<HttpResponseData> ArchiveCompletedAuctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/archive-completed")] HttpRequestData req)
    {
        _logger.LogInformation("Admin archive completed auctions request received");

        // Validate admin token
        if (!IsValidAdminRequest(req))
        {
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            // TODO: Implement actual archiving logic with database
            var archivedCount = 3; // Mock count

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true, archivedCount, message = $"Archived {archivedCount} completed auctions" }));
            response.Headers.Add("Content-Type", "application/json");

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving completed auctions");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Failed to archive auctions");
            return errorResponse;
        }
    }

    private bool IsValidAdminRequest(HttpRequestData req)
    {
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Invalid management token: {ErrorMessage}", validation.ErrorMessage);
        }
        return validation.IsValid;
    }
}

public class AuctionSummary
{
    public int AuctionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastActivity { get; set; }
}