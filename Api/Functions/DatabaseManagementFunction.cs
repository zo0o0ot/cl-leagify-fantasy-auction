using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using LeagifyFantasyAuction.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Functions for database management and migration operations.
/// Provides endpoints for applying migrations and database maintenance.
/// </summary>
public class DatabaseManagementFunction(ILoggerFactory loggerFactory, LeagifyAuctionDbContext context)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DatabaseManagementFunction>();

    /// <summary>
    /// Apply pending database migrations manually.
    /// This is needed for production deployments in Azure Static Web Apps.
    /// </summary>
    [Function("ApplyMigrations")]
    public async Task<HttpResponseData> ApplyMigrations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migrate")] HttpRequestData req)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
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

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = pendingMigrations.Any() 
                    ? $"Applied {pendingMigrations.Count()} migrations successfully"
                    : "Database is up to date",
                AppliedMigrations = pendingMigrations.ToArray()
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying database migrations");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                $"Migration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current database migration status.
    /// Shows applied and pending migrations.
    /// </summary>
    [Function("GetMigrationStatus")]
    public async Task<HttpResponseData> GetMigrationStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/migration-status")] HttpRequestData req)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                DatabaseExists = await context.Database.CanConnectAsync(),
                AppliedMigrations = appliedMigrations.ToArray(),
                PendingMigrations = pendingMigrations.ToArray(),
                TotalApplied = appliedMigrations.Count(),
                TotalPending = pendingMigrations.Count()
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                $"Failed to get migration status: {ex.Message}");
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

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}