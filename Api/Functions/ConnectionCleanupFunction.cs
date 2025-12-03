using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using System.Text.Json;
using System.Net;
using System.Net.Http;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Background function to clean up idle SignalR connections and prevent database from staying active 24/7.
/// Runs every 5 minutes to check for connections idle longer than 10 minutes.
/// </summary>
public class ConnectionCleanupFunction
{
    private readonly ILogger<ConnectionCleanupFunction> _logger;
    private readonly LeagifyAuctionDbContext _context;

    // Connection timeout thresholds
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ZombieConnectionTimeout = TimeSpan.FromMinutes(30);

    public ConnectionCleanupFunction(
        ILogger<ConnectionCleanupFunction> logger,
        LeagifyAuctionDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// HTTP-triggered function to clean up idle connections.
    /// Called by Azure Logic App every 5 minutes to automatically clean up idle connections.
    /// Can also be called manually for testing or immediate cleanup.
    /// </summary>
    [Function("CleanupIdleConnections")]
    public async Task<HttpResponseData> CleanupIdleConnections(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get", Route = "system/cleanup-connections")] HttpRequestData req)
    {
        _logger.LogInformation("🧹 Manual idle connection cleanup triggered at {Time}", DateTime.UtcNow);

        try
        {
            var result = await PerformCleanup();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during idle connection cleanup");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                Success = false,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Core cleanup logic used by both timer and HTTP triggers.
    /// Finds and disconnects idle connections.
    /// </summary>
    private async Task<CleanupResult> PerformCleanup()
    {
        var cutoffTime = DateTime.UtcNow.Subtract(IdleTimeout);
        var zombieCutoffTime = DateTime.UtcNow.Subtract(ZombieConnectionTimeout);

        // Find connections that are marked as connected but haven't been active recently
        var idleConnections = await _context.Users
            .Where(u => u.IsConnected && u.LastActiveDate < cutoffTime)
            .ToListAsync();

        // Find zombie connections (very old, likely leaked)
        var zombieConnections = await _context.Users
            .Where(u => u.IsConnected && u.LastActiveDate < zombieCutoffTime)
            .ToListAsync();

        if (idleConnections.Any())
        {
            _logger.LogInformation(
                "Found {IdleCount} idle connections (inactive > {Minutes} min) and {ZombieCount} zombie connections (inactive > {ZombieMinutes} min)",
                idleConnections.Count,
                IdleTimeout.TotalMinutes,
                zombieConnections.Count,
                ZombieConnectionTimeout.TotalMinutes);

            foreach (var user in idleConnections)
            {
                var idleMinutes = (DateTime.UtcNow - user.LastActiveDate).TotalMinutes;
                var isZombie = zombieConnections.Contains(user);

                _logger.LogInformation(
                    "{ConnectionType} User {UserId} ({DisplayName}) in auction {AuctionId} - idle for {IdleMinutes:F1} minutes",
                    isZombie ? "🧟 ZOMBIE" : "💤 IDLE",
                    user.UserId,
                    user.DisplayName,
                    user.AuctionId,
                    idleMinutes);

                // Mark as disconnected
                user.IsConnected = false;
                user.ConnectionId = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Cleaned up {Count} idle connections ({ZombieCount} zombies)",
                idleConnections.Count,
                zombieConnections.Count);
        }
        else
        {
            _logger.LogInformation("✅ No idle connections found - all connections are active");
        }

        // Log connection statistics
        await LogConnectionStatistics();

        return new CleanupResult
        {
            Success = true,
            CleanedConnections = idleConnections.Count,
            ZombieConnections = zombieConnections.Count,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Logs current connection statistics for monitoring.
    /// </summary>
    private async Task LogConnectionStatistics()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var connectedUsers = await _context.Users.CountAsync(u => u.IsConnected);
            var activeAuctions = await _context.Auctions
                .Where(a => a.Status == "InProgress" || a.Status == "Draft")
                .CountAsync();

            // Check if database should be able to auto-pause
            var canAutoPause = connectedUsers == 0;

            _logger.LogInformation(
                "📊 Connection Stats: {Connected}/{Total} users connected | {Auctions} active auctions | Database can auto-pause: {CanPause}",
                connectedUsers,
                totalUsers,
                activeAuctions,
                canAutoPause ? "YES ✅" : "NO ⚠️");

            if (!canAutoPause)
            {
                // Log which auctions have active connections
                var auctionsWithConnections = await _context.Users
                    .Where(u => u.IsConnected)
                    .GroupBy(u => u.AuctionId)
                    .Select(g => new { AuctionId = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var auction in auctionsWithConnections)
                {
                    _logger.LogInformation(
                        "  📌 Auction {AuctionId} has {Count} active connection(s)",
                        auction.AuctionId,
                        auction.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging connection statistics");
        }
    }

    /// <summary>
    /// Manual endpoint to force cleanup of all idle connections (for testing).
    /// Requires management authentication.
    /// </summary>
    [Function("ForceCleanupConnections")]
    public async Task<HttpResponseData> ForceCleanup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/cleanup-connections")] HttpRequestData req)
    {
        _logger.LogInformation("🔧 Manual connection cleanup triggered");

        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Management authentication required");
                return unauthorizedResponse;
            }

            // Forward to main cleanup endpoint
            var cleanupReq = req;
            var result = await CleanupIdleConnections(cleanupReq);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "Connection cleanup completed",
                Timestamp = DateTime.UtcNow
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual connection cleanup");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Cleanup failed: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Get current connection status and statistics (for monitoring dashboard).
    /// </summary>
    [Function("GetConnectionStatistics")]
    public async Task<HttpResponseData> GetStatistics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/connection-statistics")] HttpRequestData req)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Management authentication required");
                return unauthorizedResponse;
            }

            var now = DateTime.UtcNow;
            var idleCutoff = now.Subtract(IdleTimeout);
            var zombieCutoff = now.Subtract(ZombieConnectionTimeout);

            var stats = new
            {
                TotalUsers = await _context.Users.CountAsync(),
                ConnectedUsers = await _context.Users.CountAsync(u => u.IsConnected),
                IdleConnections = await _context.Users.CountAsync(u => u.IsConnected && u.LastActiveDate < idleCutoff),
                ZombieConnections = await _context.Users.CountAsync(u => u.IsConnected && u.LastActiveDate < zombieCutoff),
                ActiveAuctions = await _context.Auctions.CountAsync(a => a.Status == "InProgress" || a.Status == "Draft"),
                CanAutoPause = await _context.Users.CountAsync(u => u.IsConnected) == 0,
                IdleTimeoutMinutes = IdleTimeout.TotalMinutes,
                ZombieTimeoutMinutes = ZombieConnectionTimeout.TotalMinutes,
                Timestamp = now,

                AuctionBreakdown = await _context.Users
                    .Where(u => u.IsConnected)
                    .GroupBy(u => u.AuctionId)
                    .Select(g => new
                    {
                        AuctionId = g.Key,
                        ConnectedUsers = g.Count(),
                        OldestActivity = g.Min(u => u.LastActiveDate)
                    })
                    .ToListAsync()
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(stats);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection statistics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Failed to get statistics: {ex.Message}");
            return errorResponse;
        }
    }

    private async Task<bool> ValidateManagementAuth(HttpRequestData req)
    {
        try
        {
            var token = req.Headers.FirstOrDefault(h => h.Key == "X-Management-Token").Value?.FirstOrDefault();
            var expectedPassword = Environment.GetEnvironmentVariable("MANAGEMENT_PASSWORD");

            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(expectedPassword) && token == expectedPassword;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Result of connection cleanup operation.
    /// </summary>
    private class CleanupResult
    {
        public bool Success { get; set; }
        public int CleanedConnections { get; set; }
        public int ZombieConnections { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
