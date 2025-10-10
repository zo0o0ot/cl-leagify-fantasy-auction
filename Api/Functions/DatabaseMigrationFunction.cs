using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using LeagifyFantasyAuction.Api.Data;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Function for managing database migrations.
/// Provides endpoints to apply pending migrations automatically.
/// </summary>
public class DatabaseMigrationFunction
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<DatabaseMigrationFunction> _logger;

    public DatabaseMigrationFunction(LeagifyAuctionDbContext context, ILogger<DatabaseMigrationFunction> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Applies all pending Entity Framework migrations to the database.
    /// This endpoint should be called after deployment to ensure database schema is up to date.
    /// Requires management token for security.
    /// </summary>
    [Function("ApplyMigrations")]
    public async Task<HttpResponseData> ApplyMigrations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "database/migrate")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Migration request received");

            // Validate management token
            var validation = ManagementAuthFunction.ValidateManagementToken(req);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Unauthorized migration attempt: {ErrorMessage}", validation.ErrorMessage);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync($"Unauthorized: {validation.ErrorMessage}");
                return unauthorizedResponse;
            }

            _logger.LogInformation("Management token validated. Checking for pending migrations...");

            // Get pending migrations
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            var pendingMigrationsList = pendingMigrations.ToList();

            if (!pendingMigrationsList.Any())
            {
                _logger.LogInformation("No pending migrations found. Database is up to date.");
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    Success = true,
                    Message = "Database is already up to date",
                    PendingMigrations = 0,
                    AppliedMigrations = Array.Empty<string>()
                });
                return response;
            }

            _logger.LogInformation("Found {Count} pending migrations: {Migrations}",
                pendingMigrationsList.Count,
                string.Join(", ", pendingMigrationsList));

            // Apply migrations
            _logger.LogInformation("Applying migrations to database...");
            await _context.Database.MigrateAsync();

            _logger.LogInformation("✅ Successfully applied {Count} migrations", pendingMigrationsList.Count);

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                Success = true,
                Message = $"Successfully applied {pendingMigrationsList.Count} migration(s)",
                PendingMigrations = pendingMigrationsList.Count,
                AppliedMigrations = pendingMigrationsList
            });
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error applying database migrations");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error applying migrations: {ex.Message}\n\nStack trace: {ex.StackTrace}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Gets information about the current database migration status.
    /// Shows applied and pending migrations without making any changes.
    /// </summary>
    [Function("GetMigrationStatus")]
    public async Task<HttpResponseData> GetMigrationStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "database/migration-status")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Migration status request received");

            // Validate management token
            var validation = ManagementAuthFunction.ValidateManagementToken(req);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Unauthorized migration status check: {ErrorMessage}", validation.ErrorMessage);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync($"Unauthorized: {validation.ErrorMessage}");
                return unauthorizedResponse;
            }

            // Get migration information
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();

            var pendingList = pendingMigrations.ToList();
            var appliedList = appliedMigrations.ToList();

            _logger.LogInformation("Migration status: {AppliedCount} applied, {PendingCount} pending",
                appliedList.Count, pendingList.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                DatabaseUpToDate = !pendingList.Any(),
                AppliedMigrations = new
                {
                    Count = appliedList.Count,
                    Migrations = appliedList
                },
                PendingMigrations = new
                {
                    Count = pendingList.Count,
                    Migrations = pendingList
                },
                CanConnect = await CanConnectToDatabase()
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration status");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error getting migration status: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Tests database connectivity without making any changes.
    /// </summary>
    private async Task<bool> CanConnectToDatabase()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }
}
