using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace LeagifyFantasyAuction.Api.Functions;

public class AdminDashboardFunction
{
    private readonly ILogger<AdminDashboardFunction> _logger;

    public AdminDashboardFunction(ILogger<AdminDashboardFunction> logger)
    {
        _logger = logger;
    }

    [Function("GetAuctions")]
    public async Task<HttpResponseData> GetAuctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "admin/auctions")] HttpRequestData req)
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "admin/auctions/{auctionId:int}")] HttpRequestData req,
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
            // TODO: Implement actual auction deletion with database
            _logger.LogInformation($"Would delete auction {auctionId}");

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/archive-completed")] HttpRequestData req)
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
        // Check for admin token in Authorization header
        if (!req.Headers.TryGetValues("Authorization", out var authHeaderValues))
            return false;

        var authHeader = authHeaderValues.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return false;

        var token = authHeader.Substring("Bearer ".Length);

        try
        {
            // Decode and validate token (simple implementation)
            var decodedBytes = Convert.FromBase64String(token);
            var decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);
            var parts = decodedString.Split(':');

            if (parts.Length == 2 && parts[0] == "admin" && DateTime.TryParse(parts[1], out var expiryDate))
            {
                return DateTime.UtcNow < expiryDate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid admin token format");
        }

        return false;
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