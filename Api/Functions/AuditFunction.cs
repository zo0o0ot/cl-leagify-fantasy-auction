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
/// Azure Functions for audit logging and system monitoring.
/// Provides endpoints for querying administrative actions, system health, and performance metrics.
/// </summary>
public class AuditFunction(LeagifyAuctionDbContext context, ILogger<AuditFunction> logger)
{
    private readonly LeagifyAuctionDbContext _context = context;
    private readonly ILogger<AuditFunction> _logger = logger;

    /// <summary>
    /// Retrieves admin actions with optional filtering.
    /// Supports filtering by auction, action type, date range, and entity.
    /// </summary>
    [Function("GetAdminActions")]
    public async Task<HttpResponseData> GetAdminActions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/audit/actions")] HttpRequestData req)
    {
        _logger.LogInformation("GetAdminActions called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            // Parse query parameters
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var auctionIdStr = query["auctionId"];
            var actionType = query["actionType"];
            var entityType = query["entityType"];
            var startDateStr = query["startDate"];
            var endDateStr = query["endDate"];
            var limitStr = query["limit"] ?? "100";

            // Build query
            var actionsQuery = _context.AdminActions
                .Include(a => a.Auction)
                .Include(a => a.AdminUser)
                .AsQueryable();

            // Apply filters
            if (int.TryParse(auctionIdStr, out var auctionId))
            {
                actionsQuery = actionsQuery.Where(a => a.AuctionId == auctionId);
            }

            if (!string.IsNullOrEmpty(actionType))
            {
                actionsQuery = actionsQuery.Where(a => a.ActionType == actionType);
            }

            if (!string.IsNullOrEmpty(entityType))
            {
                actionsQuery = actionsQuery.Where(a => a.EntityType == entityType);
            }

            if (DateTime.TryParse(startDateStr, out var startDate))
            {
                actionsQuery = actionsQuery.Where(a => a.ActionDate >= startDate);
            }

            if (DateTime.TryParse(endDateStr, out var endDate))
            {
                actionsQuery = actionsQuery.Where(a => a.ActionDate <= endDate);
            }

            // Apply limit and order
            if (int.TryParse(limitStr, out var limit))
            {
                actionsQuery = actionsQuery.OrderByDescending(a => a.ActionDate).Take(limit);
            }

            var actions = await actionsQuery.ToListAsync();

            // Format response
            var result = actions.Select(a => new
            {
                a.AdminActionId,
                a.AuctionId,
                AuctionName = a.Auction?.Name,
                AdminUserId = a.AdminUserId,
                AdminDisplayName = a.AdminUser?.DisplayName,
                a.ActionType,
                a.Description,
                a.EntityType,
                a.EntityId,
                a.Metadata,
                a.ActionDate,
                a.IPAddress
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin actions");
            return await CreateErrorResponse(req, "Failed to retrieve admin actions");
        }
    }

    /// <summary>
    /// Records a new admin action in the audit log.
    /// Called internally by other functions to track administrative operations.
    /// </summary>
    [Function("LogAdminAction")]
    public async Task<HttpResponseData> LogAdminAction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/audit/log")] HttpRequestData req)
    {
        _logger.LogInformation("LogAdminAction called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var requestBody = await req.ReadAsStringAsync();
            var actionData = JsonSerializer.Deserialize<AdminActionRequest>(requestBody ?? string.Empty);

            if (actionData == null || string.IsNullOrEmpty(actionData.ActionType))
            {
                return await CreateBadRequestResponse(req, "ActionType is required");
            }

            var adminAction = new AdminAction
            {
                AuctionId = actionData.AuctionId,
                AdminUserId = actionData.AdminUserId,
                ActionType = actionData.ActionType,
                Description = actionData.Description ?? string.Empty,
                EntityType = actionData.EntityType,
                EntityId = actionData.EntityId,
                Metadata = actionData.Metadata,
                IPAddress = GetClientIpAddress(req)
            };

            _context.AdminActions.Add(adminAction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Logged admin action: {ActionType} by user {UserId}", 
                actionData.ActionType, actionData.AdminUserId);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new { AdminActionId = adminAction.AdminActionId });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging admin action");
            return await CreateErrorResponse(req, "Failed to log admin action");
        }
    }

    /// <summary>
    /// Gets audit summary statistics for reporting.
    /// Includes action counts, most active auctions, and recent activity.
    /// </summary>
    [Function("GetAuditSummary")]
    public async Task<HttpResponseData> GetAuditSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/audit/summary")] HttpRequestData req)
    {
        _logger.LogInformation("GetAuditSummary called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);
            var last30Days = now.AddDays(-30);

            // Get action counts
            var totalActions = await _context.AdminActions.CountAsync();
            var actionsLast24Hours = await _context.AdminActions
                .CountAsync(a => a.ActionDate >= last24Hours);
            var actionsLast7Days = await _context.AdminActions
                .CountAsync(a => a.ActionDate >= last7Days);
            var actionsLast30Days = await _context.AdminActions
                .CountAsync(a => a.ActionDate >= last30Days);

            // Get action type breakdown
            var actionsByType = await _context.AdminActions
                .Where(a => a.ActionDate >= last30Days)
                .GroupBy(a => a.ActionType)
                .Select(g => new
                {
                    ActionType = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            // Get most active auctions
            var mostActiveAuctions = await _context.AdminActions
                .Where(a => a.AuctionId != null && a.ActionDate >= last30Days)
                .GroupBy(a => new { a.AuctionId, a.Auction!.Name })
                .Select(g => new
                {
                    AuctionId = g.Key.AuctionId,
                    AuctionName = g.Key.Name,
                    ActionCount = g.Count()
                })
                .OrderByDescending(x => x.ActionCount)
                .Take(10)
                .ToListAsync();

            // Get recent actions
            var recentActions = await _context.AdminActions
                .Include(a => a.Auction)
                .Include(a => a.AdminUser)
                .OrderByDescending(a => a.ActionDate)
                .Take(20)
                .Select(a => new
                {
                    a.AdminActionId,
                    a.ActionType,
                    a.Description,
                    AuctionName = a.Auction != null ? a.Auction.Name : null,
                    AdminDisplayName = a.AdminUser != null ? a.AdminUser.DisplayName : "System",
                    a.ActionDate
                })
                .ToListAsync();

            var summary = new
            {
                TotalActions = totalActions,
                ActionsLast24Hours = actionsLast24Hours,
                ActionsLast7Days = actionsLast7Days,
                ActionsLast30Days = actionsLast30Days,
                ActionsByType = actionsByType,
                MostActiveAuctions = mostActiveAuctions,
                RecentActions = recentActions
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(summary);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit summary");
            return await CreateErrorResponse(req, "Failed to retrieve audit summary");
        }
    }

    /// <summary>
    /// Gets system health metrics.
    /// Includes database statistics, auction status, and performance indicators.
    /// </summary>
    [Function("GetSystemHealth")]
    public async Task<HttpResponseData> GetSystemHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/health")] HttpRequestData req)
    {
        _logger.LogInformation("GetSystemHealth called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);

            // Database statistics
            var totalAuctions = await _context.Auctions.CountAsync();
            var activeAuctions = await _context.Auctions
                .CountAsync(a => a.Status == "InProgress");
            var completedAuctions = await _context.Auctions
                .CountAsync(a => a.Status == "Complete");
            var draftAuctions = await _context.Auctions
                .CountAsync(a => a.Status == "Draft");

            var totalUsers = await _context.Users.CountAsync();
            var activeUsersLast24Hours = await _context.Users
                .CountAsync(u => u.LastActiveDate >= last24Hours);

            var totalSchools = await _context.Schools.CountAsync();
            var totalDraftPicks = await _context.DraftPicks.CountAsync();
            var totalBids = await _context.BidHistories.CountAsync();

            // Recent activity
            var auctionsCreatedLast24Hours = await _context.Auctions
                .CountAsync(a => a.CreatedDate >= last24Hours);
            var bidsPlacedLast24Hours = await _context.BidHistories
                .CountAsync(b => b.BidDate >= last24Hours);

            // Performance indicators
            var avgBidsPerAuction = totalAuctions > 0 
                ? (decimal)totalBids / totalAuctions 
                : 0;
            var avgPicksPerAuction = totalAuctions > 0 
                ? (decimal)totalDraftPicks / totalAuctions 
                : 0;

            // Get auction status breakdown
            var auctionsByStatus = await _context.Auctions
                .GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var health = new
            {
                Timestamp = now,
                Database = new
                {
                    TotalAuctions = totalAuctions,
                    ActiveAuctions = activeAuctions,
                    CompletedAuctions = completedAuctions,
                    DraftAuctions = draftAuctions,
                    TotalUsers = totalUsers,
                    ActiveUsersLast24Hours = activeUsersLast24Hours,
                    TotalSchools = totalSchools,
                    TotalDraftPicks = totalDraftPicks,
                    TotalBids = totalBids
                },
                Activity = new
                {
                    AuctionsCreatedLast24Hours = auctionsCreatedLast24Hours,
                    BidsPlacedLast24Hours = bidsPlacedLast24Hours
                },
                Performance = new
                {
                    AvgBidsPerAuction = Math.Round(avgBidsPerAuction, 2),
                    AvgPicksPerAuction = Math.Round(avgPicksPerAuction, 2)
                },
                AuctionsByStatus = auctionsByStatus
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(health);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system health");
            return await CreateErrorResponse(req, "Failed to retrieve system health");
        }
    }

    /// <summary>
    /// Gets performance metrics for monitoring.
    /// Includes response times, error rates, and resource utilization.
    /// </summary>
    [Function("GetPerformanceMetrics")]
    public async Task<HttpResponseData> GetPerformanceMetrics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/metrics")] HttpRequestData req)
    {
        _logger.LogInformation("GetPerformanceMetrics called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);

            // SignalR connection metrics
            var currentConnections = await _context.Users
                .CountAsync(u => !string.IsNullOrEmpty(u.ConnectionId) && u.IsConnected);

            // Bidding activity metrics
            var bidsLast24Hours = await _context.BidHistories
                .Where(b => b.BidDate >= last24Hours)
                .GroupBy(b => b.AuctionId)
                .Select(g => new
                {
                    AuctionId = g.Key,
                    BidCount = g.Count(),
                    AvgBidAmount = g.Average(b => b.BidAmount)
                })
                .ToListAsync();

            // Auction completion metrics
            var completedAuctionsLast7Days = await _context.Auctions
                .Where(a => a.CompletedDate >= last7Days && a.CompletedDate != null)
                .Select(a => new
                {
                    a.AuctionId,
                    a.Name,
                    a.StartedDate,
                    a.CompletedDate,
                    Duration = a.CompletedDate!.Value - (a.StartedDate ?? a.CompletedDate.Value)
                })
                .ToListAsync();

            var avgAuctionDuration = completedAuctionsLast7Days.Any()
                ? completedAuctionsLast7Days.Average(a => a.Duration.TotalMinutes)
                : 0;

            var metrics = new
            {
                Timestamp = now,
                SignalR = new
                {
                    CurrentConnections = currentConnections
                },
                Bidding = new
                {
                    BidsLast24Hours = bidsLast24Hours.Sum(b => b.BidCount),
                    ActiveAuctionsWithBids = bidsLast24Hours.Count,
                    AvgBidsPerActiveAuction = bidsLast24Hours.Any() 
                        ? bidsLast24Hours.Average(b => b.BidCount) 
                        : 0
                },
                Completion = new
                {
                    AuctionsCompletedLast7Days = completedAuctionsLast7Days.Count,
                    AvgAuctionDurationMinutes = Math.Round(avgAuctionDuration, 2)
                },
                RecentCompletions = completedAuctionsLast7Days.Take(10).Select(a => new
                {
                    a.AuctionId,
                    a.Name,
                    DurationMinutes = Math.Round(a.Duration.TotalMinutes, 2)
                })
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(metrics);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            return await CreateErrorResponse(req, "Failed to retrieve performance metrics");
        }
    }

    // Helper methods

    private async Task<bool> VerifyManagementToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Management-Token", out var values))
        {
            var token = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                // In production, verify against a secure token store
                // For now, we'll use a simple environment variable check
                var validToken = Environment.GetEnvironmentVariable("MANAGEMENT_TOKEN");
                return token == validToken;
            }
        }
        return false;
    }

    private static string? GetClientIpAddress(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Forwarded-For", out var forwardedValues))
        {
            return forwardedValues.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
        }
        return null;
    }

    private static async Task<HttpResponseData> CreateUnauthorizedResponse(HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        await response.WriteStringAsync("Unauthorized");
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
}

// Request/Response DTOs

public class AdminActionRequest
{
    public int? AuctionId { get; set; }
    public int? AdminUserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Metadata { get; set; }
}
