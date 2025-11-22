using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Provides HTTP endpoints for managing schools in the Leagify Fantasy Auction system using database storage.
/// This service handles CRUD operations for schools including creation, retrieval, updates, and CSV imports.
/// All endpoints require valid management authentication tokens.
/// </summary>
/// <remarks>
/// This implementation uses Entity Framework Core with SQL Server for persistent database storage.
/// Schools are identified by unique integer IDs and must have unique names (case-insensitive).
/// Logo management prioritizes local file storage with URL fallback for optimal performance.
/// </remarks>
public class SchoolManagementFunction
{
    private readonly ILogger<SchoolManagementFunction> _logger;
    private readonly LeagifyAuctionDbContext _context;
    private readonly ICsvImportService _csvImportService;

    /// <summary>
    /// Initializes a new instance of the SchoolManagementFunction class.
    /// </summary>
    /// <param name="logger">The logger for function operations.</param>
    /// <param name="context">The database context for school operations.</param>
    /// <param name="csvImportService">The CSV import service for bulk school operations.</param>
    public SchoolManagementFunction(
        ILogger<SchoolManagementFunction> logger, 
        LeagifyAuctionDbContext context,
        ICsvImportService csvImportService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _csvImportService = csvImportService ?? throw new ArgumentNullException(nameof(csvImportService));
    }

    /// <summary>
    /// Retrieves all schools from the database.
    /// </summary>
    /// <param name="req">The HTTP request containing management authentication headers.</param>
    /// <returns>
    /// HTTP 200 OK with an array of school objects containing SchoolId, Name, LogoURL, LogoFileName, CreatedDate, ModifiedDate, and AuctionCount.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during retrieval.
    /// </returns>
    /// <remarks>
    /// Schools are returned sorted alphabetically by name.
    /// The AuctionCount field represents how many auctions have referenced this school.
    /// Requires a valid Bearer token in the Authorization header obtained from the management authentication endpoint.
    /// </remarks>
    [Function("GetSchools")]
    public async Task<HttpResponseData> GetSchools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to get schools");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("Retrieving all schools from database");

            var schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.SchoolId,
                    s.Name,
                    s.LogoURL,
                    s.LogoFileName,
                    s.CreatedDate,
                    s.ModifiedDate,
                    AuctionCount = s.AuctionSchools.Count()
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {SchoolCount} schools from database", schools.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(schools);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schools from database");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error retrieving schools: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Creates a new school in the database.
    /// </summary>
    /// <param name="req">The HTTP request containing the school data in JSON format and management authentication headers.</param>
    /// <returns>
    /// HTTP 201 Created with the created school object and Location header on success.
    /// HTTP 400 Bad Request if the request body is invalid, empty, or contains invalid JSON.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 409 Conflict if a school with the same name already exists (case-insensitive comparison).
    /// HTTP 500 Internal Server Error if an unexpected error occurs during creation.
    /// </returns>
    /// <remarks>
    /// Expects a JSON body with Name (required), LogoURL (optional), and LogoFileName (optional) properties.
    /// School names must be unique across the system (case-insensitive comparison).
    /// The created school is assigned a unique integer ID automatically by the database.
    /// CreatedDate and ModifiedDate are set to the current UTC time.
    /// A Location header is included in the response pointing to the created resource.
    /// </remarks>
    [Function("CreateSchool")]
    public async Task<HttpResponseData> CreateSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("=== CREATE SCHOOL REQUEST STARTED (Database) ===");

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to create school");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request body length: {Length} characters", requestBody.Length);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("Empty request body received");
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }

            CreateSchoolDto? schoolDto;
            try
            {
                schoolDto = JsonSerializer.Deserialize<CreateSchoolDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation("Deserialized school - Name: '{Name}', LogoURL: '{LogoURL}'", 
                    schoolDto?.Name, schoolDto?.LogoURL);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON: {RequestBody}", requestBody);
                var jsonErrorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await jsonErrorResponse.WriteStringAsync($"Invalid JSON format: {ex.Message}");
                return jsonErrorResponse;
            }

            if (schoolDto == null || string.IsNullOrWhiteSpace(schoolDto.Name))
            {
                _logger.LogWarning("Invalid school data - missing name");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body or missing school name");
                return badResponse;
            }

            // Check for duplicate name in database
            var existingSchool = await _context.Schools
                .FirstOrDefaultAsync(s => s.Name.ToLower() == schoolDto.Name.ToLower());

            if (existingSchool != null)
            {
                _logger.LogWarning("Duplicate school name found: '{Name}'", schoolDto.Name);
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("School name already exists");
                return conflictResponse;
            }

            // Create new school
            var school = new School
            {
                Name = schoolDto.Name.Trim(),
                LogoURL = schoolDto.LogoURL?.Trim(),
                LogoFileName = schoolDto.LogoFileName?.Trim(),
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _context.Schools.Add(school);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created school {SchoolName} with ID {SchoolId}", school.Name, school.SchoolId);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new
            {
                school.SchoolId,
                school.Name,
                school.LogoURL,
                school.LogoFileName,
                school.CreatedDate,
                school.ModifiedDate
            });
            response.Headers.Add("Location", $"/api/management/schools/{school.SchoolId}");
            
            _logger.LogInformation("=== CREATE SCHOOL REQUEST COMPLETED SUCCESSFULLY ===");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== CREATE SCHOOL REQUEST FAILED WITH EXCEPTION ===");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error creating school: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Imports schools from a CSV file, replacing all existing schools in the database.
    /// Optionally downloads school logos from URLs provided in the CSV and stores them locally.
    /// </summary>
    /// <param name="req">The HTTP request containing the CSV file data and management authentication headers.</param>
    /// <returns>
    /// HTTP 200 OK with import results including total schools, successful downloads, failed downloads, and any errors.
    /// HTTP 400 Bad Request if the CSV format is invalid or contains errors.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during import.
    /// </returns>
    /// <remarks>
    /// This operation completely replaces all existing schools in the database.
    /// The CSV should contain school data with columns for Name, SchoolURL (for logos), and other school information.
    /// Logo download is enabled by default and will attempt to download images from provided SchoolURL values.
    /// Successfully downloaded logos are stored locally and LogoFileName is updated to reference the local file.
    /// Failed logo downloads are reported but do not prevent the import from succeeding.
    /// Uses the ICsvImportService to handle CSV parsing and logo downloads.
    /// </remarks>
    [Function("ImportSchoolsCsv")]
    public async Task<HttpResponseData> ImportSchoolsCsv(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools/import")] HttpRequestData req)
    {
        try
        {
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to import schools CSV");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("Starting CSV import with logo download to database");

            // Get the CSV file from the request
            using var stream = req.Body;
            var importResult = await _csvImportService.ImportCsvAsync(stream, downloadLogos: true);

            if (!importResult.IsSuccess)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new
                {
                    success = false,
                    errors = importResult.Errors
                });
                return errorResponse;
            }

            // Begin database transaction for atomic operation
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Clear existing schools from database
                var existingSchools = await _context.Schools.ToListAsync();
                _context.Schools.RemoveRange(existingSchools);

                // Add imported schools to database
                foreach (var importedSchool in importResult.Schools)
                {
                    var school = new School
                    {
                        Name = importedSchool.Name,
                        LogoURL = importedSchool.SchoolURL,
                        LogoFileName = importedSchool.LogoFileName, // This will be set if download succeeded
                        CreatedDate = DateTime.UtcNow,
                        ModifiedDate = DateTime.UtcNow
                    };

                    _context.Schools.Add(school);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("CSV import completed. Imported {SchoolCount} schools to database", 
                    importResult.TotalSchools);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    totalSchools = importResult.TotalSchools,
                    successfulDownloads = importResult.SuccessfulDownloads.Count,
                    failedDownloads = importResult.FailedDownloads.Count,
                    errors = importResult.Errors,
                    failedDownloadDetails = importResult.FailedDownloads
                });
                return response;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing schools from CSV to database");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error importing schools: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Updates an existing school's information in the database.
    /// </summary>
    /// <param name="req">The HTTP request containing the updated school data in JSON format and management authentication headers.</param>
    /// <param name="schoolId">The unique integer ID of the school to update.</param>
    /// <returns>
    /// HTTP 200 OK with the updated school object on success.
    /// HTTP 400 Bad Request if the request body is invalid, empty, or contains invalid JSON.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 404 Not Found if the specified school ID does not exist.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during update.
    /// </returns>
    /// <remarks>
    /// Expects a JSON body with Name (required) and LogoURL (optional) properties.
    /// The ModifiedDate is automatically updated to the current UTC time.
    /// LogoFileName is preserved from the original school data unless explicitly updated.
    /// School name uniqueness is not enforced during updates (this may be a design consideration for future versions).
    /// The AuctionCount field in the response represents how many auctions have referenced this school.
    /// </remarks>
    [Function("UpdateSchool")]
    public async Task<HttpResponseData> UpdateSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "management/schools/{schoolId:int}")] HttpRequestData req,
        int schoolId)
    {
        try
        {
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to update school");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("=== UPDATE SCHOOL REQUEST: ID {SchoolId} (Database) ===", schoolId);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }

            var updateDto = JsonSerializer.Deserialize<UpdateSchoolDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (updateDto == null || string.IsNullOrWhiteSpace(updateDto.Name))
            {
                var invalidResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await invalidResponse.WriteStringAsync("Invalid school data or missing name");
                return invalidResponse;
            }

            // Find school in database
            var existingSchool = await _context.Schools.FindAsync(schoolId);
            if (existingSchool == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("School not found");
                return notFoundResponse;
            }

            // Update the school
            existingSchool.Name = updateDto.Name.Trim();
            existingSchool.LogoURL = updateDto.LogoURL?.Trim();
            existingSchool.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated school: {SchoolName} (ID: {SchoolId})", 
                existingSchool.Name, schoolId);

            // Get auction count for response
            var auctionCount = await _context.AuctionSchools
                .Where(auc => auc.SchoolId == schoolId)
                .CountAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                schoolId = existingSchool.SchoolId,
                name = existingSchool.Name,
                logoURL = existingSchool.LogoURL,
                logoFileName = existingSchool.LogoFileName,
                createdDate = existingSchool.CreatedDate,
                modifiedDate = existingSchool.ModifiedDate,
                auctionCount = auctionCount
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating school {SchoolId} in database", schoolId);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error updating school: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Deletes a school from the database.
    /// </summary>
    /// <param name="req">The HTTP request containing management authentication headers.</param>
    /// <param name="schoolId">The unique integer ID of the school to delete.</param>
    /// <returns>
    /// HTTP 200 OK with a success message and deleted school information on success.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 404 Not Found if the specified school ID does not exist.
    /// HTTP 409 Conflict if the school is referenced by auction data and cannot be deleted.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during deletion.
    /// </returns>
    /// <remarks>
    /// This operation permanently removes the school from the database.
    /// Checks for references in auction systems before allowing deletion to maintain referential integrity.
    /// If the school is used in any auctions, deletion is prevented and a conflict error is returned.
    /// The response includes the deleted school's ID and name for confirmation.
    /// </remarks>
    [Function("DeleteSchool")]
    public async Task<HttpResponseData> DeleteSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "management/schools/{schoolId:int}")] HttpRequestData req,
        int schoolId)
    {
        try
        {
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to delete school");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("=== DELETE SCHOOL REQUEST: ID {SchoolId} (Database) ===", schoolId);

            var school = await _context.Schools.FindAsync(schoolId);
            if (school == null)
            {
                _logger.LogWarning("School with ID {SchoolId} not found", schoolId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("School not found");
                return notFoundResponse;
            }

            // Check if school is used in any auctions
            var auctionCount = await _context.AuctionSchools
                .Where(auc => auc.SchoolId == schoolId)
                .CountAsync();

            if (auctionCount > 0)
            {
                _logger.LogWarning("Cannot delete school {SchoolId} - referenced by {AuctionCount} auctions", 
                    schoolId, auctionCount);
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync($"Cannot delete school - it is referenced by {auctionCount} auction(s)");
                return conflictResponse;
            }

            _context.Schools.Remove(school);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted school: {SchoolName} (ID: {SchoolId})", 
                school.Name, schoolId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { 
                message = "School deleted successfully", 
                schoolId = school.SchoolId, 
                name = school.Name 
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting school {SchoolId} from database", schoolId);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error deleting school: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Validates that the HTTP request contains a valid management authentication token.
    /// </summary>
    /// <param name="req">The HTTP request to validate.</param>
    /// <returns>True if the request contains a valid management token; otherwise, false.</returns>
    /// <remarks>
    /// Uses the ManagementAuthFunction.ValidateManagementToken method to perform token validation.
    /// Logs a warning message if the token validation fails, including the specific error message.
    /// Expects a Bearer token in the Authorization header.
    /// </remarks>
    /// <summary>
    /// TEMPORARY: Apply database migrations to add SessionToken column.
    /// This will be removed once the database migration is applied.
    /// </summary>
    [Function("TempApplyMigrations")]
    public async Task<HttpResponseData> TempApplyMigrations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "temp/migrate")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("TEMPORARY: Applying database schema migrations");
            var results = new List<string>();

            // Check and add SessionToken column to Users table
            var sessionTokenExists = await CheckColumnExists("Users", "SessionToken");
            if (!sessionTokenExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [Users] ADD [SessionToken] nvarchar(200) NULL");
                results.Add("✓ Added SessionToken column to Users table");
                _logger.LogInformation("Successfully added SessionToken column to Users table");
            }
            else
            {
                results.Add("SessionToken column already exists in Users table");
            }

            // Check and add HasTestedBidding column to Users table
            var hasTestedBiddingExists = await CheckColumnExists("Users", "HasTestedBidding");
            if (!hasTestedBiddingExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [Users] ADD [HasTestedBidding] bit NOT NULL DEFAULT 0");
                results.Add("✓ Added HasTestedBidding column to Users table");
                _logger.LogInformation("Successfully added HasTestedBidding column to Users table");
            }
            else
            {
                results.Add("HasTestedBidding column already exists in Users table");
            }

            // Check and add IsReadyToDraft column to Users table
            var isReadyToDraftExists = await CheckColumnExists("Users", "IsReadyToDraft");
            if (!isReadyToDraftExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [Users] ADD [IsReadyToDraft] bit NOT NULL DEFAULT 0");
                results.Add("✓ Added IsReadyToDraft column to Users table");
                _logger.LogInformation("Successfully added IsReadyToDraft column to Users table");
            }
            else
            {
                results.Add("IsReadyToDraft column already exists in Users table");
            }

            // Check and add HasPassedOnTestBid column to Users table
            var hasPassedOnTestBidExists = await CheckColumnExists("Users", "HasPassedOnTestBid");
            if (!hasPassedOnTestBidExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [Users] ADD [HasPassedOnTestBid] bit NOT NULL DEFAULT 0");
                results.Add("✓ Added HasPassedOnTestBid column to Users table");
                _logger.LogInformation("Successfully added HasPassedOnTestBid column to Users table");
            }
            else
            {
                results.Add("HasPassedOnTestBid column already exists in Users table");
            }

            // Check and add new AdminAction columns
            var entityTypeExists = await CheckColumnExists("AdminActions", "EntityType");
            if (!entityTypeExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [AdminActions] ADD [EntityType] nvarchar(50) NULL");
                results.Add("✓ Added EntityType column to AdminActions table");
                _logger.LogInformation("Successfully added EntityType column to AdminActions table");
            }
            else
            {
                results.Add("EntityType column already exists in AdminActions table");
            }

            var entityIdExists = await CheckColumnExists("AdminActions", "EntityId");
            if (!entityIdExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [AdminActions] ADD [EntityId] int NULL");
                results.Add("✓ Added EntityId column to AdminActions table");
                _logger.LogInformation("Successfully added EntityId column to AdminActions table");
            }
            else
            {
                results.Add("EntityId column already exists in AdminActions table");
            }

            var metadataExists = await CheckColumnExists("AdminActions", "Metadata");
            if (!metadataExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [AdminActions] ADD [Metadata] nvarchar(max) NULL");
                results.Add("✓ Added Metadata column to AdminActions table");
                _logger.LogInformation("Successfully added Metadata column to AdminActions table");
            }
            else
            {
                results.Add("Metadata column already exists in AdminActions table");
            }

            // Check and add CurrentTestSchoolId column to Auctions table
            var currentTestSchoolIdExists = await CheckColumnExists("Auctions", "CurrentTestSchoolId");
            if (!currentTestSchoolIdExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [Auctions] ADD [CurrentTestSchoolId] int NOT NULL DEFAULT -1");
                results.Add("✓ Added CurrentTestSchoolId column to Auctions table");
                _logger.LogInformation("Successfully added CurrentTestSchoolId column to Auctions table");
            }
            else
            {
                results.Add("CurrentTestSchoolId column already exists in Auctions table");
            }

            // Check and add UseManagementAsAdmin column to Auctions table
            var useManagementAsAdminExists = await CheckColumnExists("Auctions", "UseManagementAsAdmin");
            if (!useManagementAsAdminExists)
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE [Auctions] ADD [UseManagementAsAdmin] bit NOT NULL DEFAULT 0");
                results.Add("✓ Added UseManagementAsAdmin column to Auctions table");
                _logger.LogInformation("Successfully added UseManagementAsAdmin column to Auctions table");
            }
            else
            {
                results.Add("UseManagementAsAdmin column already exists in Auctions table");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                Message = "Schema migration completed",
                Results = results
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying schema migrations");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = $"Migration failed: {ex.Message}" });
            return errorResponse;
        }
    }

    private async Task<bool> CheckColumnExists(string tableName, string columnName)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                $"SELECT TOP 1 [{columnName}] FROM [{tableName}] WHERE 1=0");
            return true;
        }
        catch
        {
            return false;
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

/// <summary>
/// Data transfer object for creating a new school.
/// </summary>
/// <remarks>
/// This class is used to deserialize JSON data from HTTP requests when creating new schools.
/// All validation should be performed on the server side after deserialization.
/// </remarks>
public class CreateSchoolDto
{
    /// <summary>
    /// Gets or sets the name of the school to be created.
    /// </summary>
    /// <value>The full name of the school. This field is required and cannot be empty.</value>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the URL to the school's logo image.
    /// </summary>
    /// <value>A valid URL pointing to the school's logo image, or null if no logo is provided.</value>
    public string? LogoURL { get; set; }
    
    /// <summary>
    /// Gets or sets the filename for the locally stored logo image.
    /// </summary>
    /// <value>The filename of the logo image stored locally, or null if no local file exists.</value>
    public string? LogoFileName { get; set; }
}

/// <summary>
/// Data transfer object for updating an existing school.
/// </summary>
/// <remarks>
/// This class is used to deserialize JSON data from HTTP requests when updating schools.
/// Only the fields that need to be updated should be included in the request.
/// </remarks>
public class UpdateSchoolDto
{
    /// <summary>
    /// Gets or sets the updated name of the school.
    /// </summary>
    /// <value>The new name for the school. This field is required and cannot be empty.</value>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the updated URL to the school's logo image.
    /// </summary>
    /// <value>The new URL pointing to the school's logo image, or null to clear the logo URL.</value>
    public string? LogoURL { get; set; }
}