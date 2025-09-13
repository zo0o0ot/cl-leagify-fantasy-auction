using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using LeagifyFantasyAuction.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagifyFantasyAuction.Api.Functions;

public class AdminTestFunction(ILogger<AdminTestFunction> logger, LeagifyAuctionDbContext context)
{
    private readonly ILogger<AdminTestFunction> _logger = logger;

    [Function("AdminTest")]
    public async Task<HttpResponseData> AdminTest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/test")] HttpRequestData req)
    {
        _logger.LogInformation("Admin test function executed");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Admin functions are working!");
        return response;
    }

    [Function("ApplyMigrations")]
    public async Task<HttpResponseData> ApplyMigrations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/migrate")] HttpRequestData req)
    {
        try
        {
            // Validate management authentication
            var token = req.Headers.FirstOrDefault(h => h.Key == "X-Management-Token").Value?.FirstOrDefault();
            var expectedPassword = Environment.GetEnvironmentVariable("MANAGEMENT_PASSWORD");
            
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expectedPassword) || token != expectedPassword)
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                await response.WriteStringAsync("Management authentication required");
                return response;
            }

            _logger.LogInformation("Starting database migration process");

            // Apply any pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Found {Count} pending migrations: {Migrations}", 
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                
                await context.Database.MigrateAsync();
                
                _logger.LogInformation("Successfully applied {Count} migrations", pendingMigrations.Count());
            }
            else
            {
                _logger.LogInformation("No pending migrations found. Database is up to date.");
            }

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                Success = true,
                Message = pendingMigrations.Any() 
                    ? $"Applied {pendingMigrations.Count()} migrations successfully"
                    : "Database is up to date",
                AppliedMigrations = pendingMigrations.ToArray()
            });

            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying database migrations");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = $"Migration failed: {ex.Message}" });
            return errorResponse;
        }
    }

    [Function("GetMigrationStatus")]
    public async Task<HttpResponseData> GetMigrationStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/migration-status")] HttpRequestData req)
    {
        try
        {
            // Validate management authentication
            var token = req.Headers.FirstOrDefault(h => h.Key == "X-Management-Token").Value?.FirstOrDefault();
            var expectedPassword = Environment.GetEnvironmentVariable("MANAGEMENT_PASSWORD");
            
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(expectedPassword) || token != expectedPassword)
            {
                var response = req.CreateResponse(HttpStatusCode.Unauthorized);
                await response.WriteStringAsync("Management authentication required");
                return response;
            }

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                DatabaseExists = await context.Database.CanConnectAsync(),
                AppliedMigrations = appliedMigrations.ToArray(),
                PendingMigrations = pendingMigrations.ToArray(),
                TotalApplied = appliedMigrations.Count(),
                TotalPending = pendingMigrations.Count()
            });

            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = $"Failed to get migration status: {ex.Message}" });
            return errorResponse;
        }
    }
}