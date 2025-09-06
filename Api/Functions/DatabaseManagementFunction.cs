using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Net;
using LeagifyFantasyAuction.Api.Data;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Provides database management endpoints for development and troubleshooting.
/// These endpoints help with database initialization, reset, and schema management.
/// </summary>
/// <remarks>
/// This function is intended for development use and should be secured or removed in production.
/// It provides utilities to recreate database schema when needed.
/// </remarks>
public class DatabaseManagementFunction
{
    private readonly ILogger<DatabaseManagementFunction> _logger;
    private readonly LeagifyAuctionDbContext _context;

    /// <summary>
    /// Initializes a new instance of the DatabaseManagementFunction class.
    /// </summary>
    /// <param name="logger">The logger for function operations.</param>
    /// <param name="context">The database context for database operations.</param>
    public DatabaseManagementFunction(ILogger<DatabaseManagementFunction> logger, LeagifyAuctionDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Creates the database and all tables if they don't exist.
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <returns>HTTP 200 OK if database creation succeeded, HTTP 500 Internal Server Error if failed.</returns>
    /// <remarks>
    /// This endpoint uses EnsureCreated() which creates the database and tables but won't apply migrations.
    /// Safe to call multiple times - won't affect existing data.
    /// </remarks>
    [Function("CreateDatabase")]
    public async Task<HttpResponseData> CreateDatabase(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dev/database/create")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Creating database if it doesn't exist...");
            
            var created = await _context.Database.EnsureCreatedAsync();
            
            if (created)
            {
                _logger.LogInformation("Database created successfully");
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Database created successfully", created = true });
                return response;
            }
            else
            {
                _logger.LogInformation("Database already exists");
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = true, message = "Database already exists", created = false });
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { success = false, error = ex.Message, details = ex.ToString() });
            return response;
        }
    }

    /// <summary>
    /// Recreates the database by dropping and creating it fresh.
    /// WARNING: This will delete all data!
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <returns>HTTP 200 OK if database recreation succeeded, HTTP 500 Internal Server Error if failed.</returns>
    /// <remarks>
    /// This endpoint uses EnsureDeleted() followed by EnsureCreated().
    /// WARNING: This will permanently delete all data in the database!
    /// Use with extreme caution.
    /// </remarks>
    [Function("RecreateDatabase")]
    public async Task<HttpResponseData> RecreateDatabase(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dev/database/recreate")] HttpRequestData req)
    {
        try
        {
            _logger.LogWarning("RECREATING DATABASE - THIS WILL DELETE ALL DATA!");
            
            // Delete the database if it exists
            var deleted = await _context.Database.EnsureDeletedAsync();
            _logger.LogInformation($"Database deleted: {deleted}");
            
            // Create the database fresh
            var created = await _context.Database.EnsureCreatedAsync();
            _logger.LogInformation($"Database created: {created}");
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { 
                success = true, 
                message = "Database recreated successfully - ALL DATA WAS DELETED!", 
                deleted = deleted,
                created = created
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recreating database");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { success = false, error = ex.Message, details = ex.ToString() });
            return response;
        }
    }

    /// <summary>
    /// Checks if the database can be connected to and gets basic information.
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <returns>HTTP 200 OK with database info, HTTP 500 Internal Server Error if connection failed.</returns>
    /// <remarks>
    /// This endpoint tests database connectivity and provides diagnostic information.
    /// Useful for troubleshooting connection issues.
    /// </remarks>
    [Function("DatabaseInfo")]
    public async Task<HttpResponseData> GetDatabaseInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dev/database/info")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Checking database connection and info...");
            
            // Test basic connection
            var canConnect = await _context.Database.CanConnectAsync();
            var connectionString = _context.Database.GetConnectionString();
            var providerName = _context.Database.ProviderName;
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { 
                success = true,
                canConnect = canConnect,
                providerName = providerName,
                connectionString = MaskConnectionString(connectionString),
                timestamp = DateTime.UtcNow
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database info");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { 
                success = false, 
                error = ex.Message, 
                connectionString = MaskConnectionString(_context.Database.GetConnectionString()),
                providerName = _context.Database.ProviderName
            });
            return response;
        }
    }

    /// <summary>
    /// Masks sensitive information in connection strings for safe logging.
    /// </summary>
    /// <param name="connectionString">The connection string to mask.</param>
    /// <returns>The masked connection string with sensitive data replaced.</returns>
    private static string? MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        // Mask passwords and other sensitive data
        return connectionString
            .Replace(connectionString.Contains("Password=") ? 
                connectionString.Substring(connectionString.IndexOf("Password=")) : "", 
                "Password=***")
            .Replace(connectionString.Contains("pwd=") ? 
                connectionString.Substring(connectionString.IndexOf("pwd=")) : "", 
                "pwd=***");
    }
}