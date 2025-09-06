using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Provides HTTP endpoints for managing schools in the Leagify Fantasy Auction system.
/// This service handles CRUD operations for schools including creation, retrieval, updates, and CSV imports.
/// All endpoints require valid management authentication tokens.
/// </summary>
/// <remarks>
/// This implementation uses in-memory storage for development and testing purposes.
/// In production, this should be replaced with persistent database storage.
/// Schools are identified by unique integer IDs and must have unique names (case-insensitive).
/// </remarks>
public class SimpleSchoolManagementFunction(ILogger<SimpleSchoolManagementFunction> logger, ICsvImportService csvImportService)
{
    private readonly ILogger<SimpleSchoolManagementFunction> _logger = logger;
    private readonly ICsvImportService _csvImportService = csvImportService;
    
    // In-memory storage for now (this will be lost on restart, but works for testing)
    private static readonly ConcurrentDictionary<int, SchoolData> _schools = new();
    private static int _nextId = 1;
    private static readonly object _idLock = new object();

    /// <summary>
    /// Retrieves all schools from the system.
    /// </summary>
    /// <param name="req">The HTTP request containing management authentication headers.</param>
    /// <returns>
    /// HTTP 200 OK with an array of school objects containing SchoolId, Name, LogoURL, LogoFileName, CreatedDate, ModifiedDate, and AuctionCount.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during retrieval.
    /// </returns>
    /// <remarks>
    /// Schools are returned sorted alphabetically by name.
    /// The AuctionCount field is currently hardcoded to 0 as auction functionality is not yet implemented.
    /// Requires a valid Bearer token in the Authorization header obtained from the management authentication endpoint.
    /// </remarks>
    [Function("GetSchoolsSimple")]
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
            
            var schools = _schools.Values
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.SchoolId,
                    s.Name,
                    s.LogoURL,
                    s.LogoFileName,
                    s.CreatedDate,
                    s.ModifiedDate,
                    AuctionCount = 0 // No auctions yet
                })
                .ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(schools);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schools");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error retrieving schools: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Creates a new school in the system.
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
    /// The created school is assigned a unique integer ID automatically.
    /// CreatedDate and ModifiedDate are set to the current UTC time.
    /// A Location header is included in the response pointing to the created resource.
    /// </remarks>
    [Function("CreateSchoolSimple")]
    public async Task<HttpResponseData> CreateSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("=== CREATE SCHOOL REQUEST STARTED ===");
            _logger.LogInformation($"Request method: {req.Method}");
            _logger.LogInformation($"Request URL: {req.Url}");
            _logger.LogInformation($"Headers: {string.Join(", ", req.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to create school");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("STEP 1: Reading request body");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"STEP 1 SUCCESS: Request body length: {requestBody.Length} characters");
            _logger.LogInformation($"STEP 1 SUCCESS: Request body content: '{requestBody}'");
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("STEP 1 FAILED: Empty request body received");
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }
            
            _logger.LogInformation("STEP 2: Deserializing JSON");
            CreateSchoolDto? schoolDto;
            try
            {
                schoolDto = JsonSerializer.Deserialize<CreateSchoolDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation($"STEP 2 SUCCESS: Deserialized school - Name: '{schoolDto?.Name}', LogoURL: '{schoolDto?.LogoURL}', LogoFileName: '{schoolDto?.LogoFileName}'");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "STEP 2 FAILED: Failed to deserialize JSON: {RequestBody}", requestBody);
                var jsonErrorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await jsonErrorResponse.WriteStringAsync($"Invalid JSON format: {ex.Message}");
                return jsonErrorResponse;
            }

            _logger.LogInformation("STEP 3: Validating school data");
            if (schoolDto == null || string.IsNullOrEmpty(schoolDto.Name))
            {
                _logger.LogWarning("STEP 3 FAILED: Invalid school data - schoolDto is null or Name is empty");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body or missing school name");
                return badResponse;
            }
            _logger.LogInformation($"STEP 3 SUCCESS: School name validated: '{schoolDto.Name}'");

            _logger.LogInformation($"STEP 4: Checking for duplicate name in {_schools.Count} existing schools");
            // Check for duplicate name
            if (_schools.Values.Any(s => s.Name.Equals(schoolDto.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning($"STEP 4 FAILED: Duplicate school name found: '{schoolDto.Name}'");
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("School name already exists");
                return conflictResponse;
            }
            _logger.LogInformation($"STEP 4 SUCCESS: No duplicate found for '{schoolDto.Name}'");

            _logger.LogInformation("STEP 5: Creating school object");
            var school = new SchoolData
            {
                SchoolId = GetNextId(),
                Name = schoolDto.Name,
                LogoURL = schoolDto.LogoURL,
                LogoFileName = schoolDto.LogoFileName,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };
            _logger.LogInformation($"STEP 5 SUCCESS: School object created with ID {school.SchoolId}");

            _logger.LogInformation($"STEP 6: Adding school to dictionary (current count: {_schools.Count})");
            var addResult = _schools.TryAdd(school.SchoolId, school);
            _logger.LogInformation($"STEP 6 RESULT: TryAdd returned {addResult}, new count: {_schools.Count}");

            _logger.LogInformation("STEP 7: Preparing response");
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
            _logger.LogInformation("STEP 7 SUCCESS: Response prepared and returned");
            _logger.LogInformation("=== CREATE SCHOOL REQUEST COMPLETED SUCCESSFULLY ===");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== CREATE SCHOOL REQUEST FAILED WITH EXCEPTION ===");
            _logger.LogError(ex, "Exception details: Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                ex.GetType().Name, ex.Message, ex.StackTrace);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error creating school: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Imports schools from a CSV file, replacing all existing schools in the system.
    /// Optionally downloads school logos from URLs provided in the CSV.
    /// </summary>
    /// <param name="req">The HTTP request containing the CSV file data and management authentication headers.</param>
    /// <returns>
    /// HTTP 200 OK with import results including total schools, successful downloads, failed downloads, and any errors.
    /// HTTP 400 Bad Request if the CSV format is invalid or contains errors.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during import.
    /// </returns>
    /// <remarks>
    /// This operation completely replaces all existing schools in the system.
    /// The CSV should contain school data with columns for Name, SchoolURL (for logos), and other school information.
    /// Logo download is enabled by default and will attempt to download images from provided SchoolURL values.
    /// Failed logo downloads are reported but do not prevent the import from succeeding.
    /// School IDs are reassigned starting from 1 after import.
    /// Uses the ICsvImportService to handle CSV parsing and logo downloads.
    /// </remarks>
    [Function("ImportSchoolsCsvSimple")]
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

            _logger.LogInformation("Starting CSV import with logo download");

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

            // Clear existing schools and add imported ones
            _schools.Clear();
            lock (_idLock)
            {
                _nextId = 1;
            }

            foreach (var importedSchool in importResult.Schools)
            {
                var school = new SchoolData
                {
                    SchoolId = GetNextId(),
                    Name = importedSchool.Name,
                    LogoURL = importedSchool.SchoolURL,
                    LogoFileName = importedSchool.LogoFileName,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                };

                _schools.TryAdd(school.SchoolId, school);
            }

            _logger.LogInformation("CSV import completed. Imported {SchoolCount} schools", importResult.TotalSchools);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing schools from CSV");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error importing schools: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Updates an existing school's information.
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
    /// LogoFileName is preserved from the original school data.
    /// School name uniqueness is not enforced during updates (this may be a design consideration for future versions).
    /// The AuctionCount field in the response is hardcoded to 0 as auction functionality is not yet implemented.
    /// </remarks>
    [Function("UpdateSchoolSimple")]
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

            _logger.LogInformation($"=== UPDATE SCHOOL REQUEST: ID {schoolId} ===");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"Request body: {requestBody}");

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

            // Check if school exists
            if (!_schools.TryGetValue(schoolId, out var existingSchool))
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("School not found");
                return notFoundResponse;
            }

            // Update the school
            existingSchool.Name = updateDto.Name;
            existingSchool.LogoURL = updateDto.LogoURL;
            existingSchool.ModifiedDate = DateTime.UtcNow;

            _logger.LogInformation($"Successfully updated school: {existingSchool.Name} (ID: {schoolId})");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                schoolId = existingSchool.SchoolId,
                name = existingSchool.Name,
                logoURL = existingSchool.LogoURL,
                logoFileName = existingSchool.LogoFileName,
                createdDate = existingSchool.CreatedDate,
                modifiedDate = existingSchool.ModifiedDate,
                auctionCount = 0 // TODO: Calculate when auction system is implemented
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating school {schoolId}");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error updating school: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Deletes a school from the system.
    /// </summary>
    /// <param name="req">The HTTP request containing management authentication headers.</param>
    /// <param name="schoolId">The unique integer ID of the school to delete.</param>
    /// <returns>
    /// HTTP 200 OK with a success message and deleted school information on success.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 404 Not Found if the specified school ID does not exist.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during deletion.
    /// </returns>
    /// <remarks>
    /// This operation permanently removes the school from the system.
    /// In production, this should check for references in auction systems before allowing deletion.
    /// Currently, no referential integrity checks are performed (marked as TODO).
    /// The response includes the deleted school's ID and name for confirmation.
    /// </remarks>
    [Function("DeleteSchoolSimple")]
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

            _logger.LogInformation($"=== DELETE SCHOOL REQUEST: ID {schoolId} ===");

            if (!_schools.TryGetValue(schoolId, out var school))
            {
                _logger.LogWarning($"School with ID {schoolId} not found");
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("School not found");
                return notFoundResponse;
            }

            // TODO: In production, check if school is used in any auctions
            // For now, just delete it
            if (_schools.TryRemove(schoolId, out _))
            {
                _logger.LogInformation($"Successfully deleted school: {school.Name} (ID: {schoolId})");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "School deleted successfully", schoolId, name = school.Name });
                return response;
            }
            else
            {
                _logger.LogError($"Failed to remove school {schoolId} from dictionary");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Failed to delete school");
                return errorResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting school {schoolId}");
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
    private bool IsValidAdminRequest(HttpRequestData req)
    {
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Invalid management token: {ErrorMessage}", validation.ErrorMessage);
        }
        return validation.IsValid;
    }

    /// <summary>
    /// Generates the next available unique integer ID for a new school.
    /// </summary>
    /// <returns>A unique integer ID that can be used for a new school.</returns>
    /// <remarks>
    /// This method is thread-safe and uses locking to ensure unique IDs even under concurrent access.
    /// IDs are assigned sequentially starting from 1.
    /// In production, this should be replaced with database-generated IDs.
    /// </remarks>
    private static int GetNextId()
    {
        lock (_idLock)
        {
            return _nextId++;
        }
    }

}

/// <summary>
/// Represents a school entity in the Leagify Fantasy Auction system.
/// </summary>
/// <remarks>
/// This class serves as the primary data model for schools stored in the system.
/// In production, this should be replaced with a proper Entity Framework model.
/// School names are expected to be unique across the system (case-insensitive).
/// </remarks>
public class SchoolData
{
    /// <summary>
    /// Gets or sets the unique identifier for the school.
    /// </summary>
    /// <value>A unique integer ID assigned when the school is created.</value>
    public int SchoolId { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the school.
    /// </summary>
    /// <value>The full name of the school. Must be unique across the system (case-insensitive).</value>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Gets or sets the URL to the school's logo image.
    /// </summary>
    /// <value>A valid URL pointing to the school's logo image, or null if no logo is available.</value>
    public string? LogoURL { get; set; }
    
    /// <summary>
    /// Gets or sets the filename of the downloaded logo image.
    /// </summary>
    /// <value>The local filename of the logo image after download, or null if no logo was downloaded.</value>
    public string? LogoFileName { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the school was created.
    /// </summary>
    /// <value>The UTC timestamp when the school was first added to the system.</value>
    public DateTime CreatedDate { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the school was last modified.
    /// </summary>
    /// <value>The UTC timestamp when the school was last updated.</value>
    public DateTime ModifiedDate { get; set; }
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
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Gets or sets the URL to the school's logo image.
    /// </summary>
    /// <value>A valid URL pointing to the school's logo image, or null if no logo is provided.</value>
    public string? LogoURL { get; set; }
    
    /// <summary>
    /// Gets or sets the filename for the logo image.
    /// </summary>
    /// <value>The desired filename for the logo image, or null to use a generated filename.</value>
    public string? LogoFileName { get; set; }
}

/// <summary>
/// Data transfer object for updating an existing school.
/// </summary>
/// <remarks>
/// This class is used to deserialize JSON data from HTTP requests when updating schools.
/// Only the fields that need to be updated should be included in the request.
/// The LogoFileName field is not included as it's preserved from the original school data.
/// </remarks>
public class UpdateSchoolDto
{
    /// <summary>
    /// Gets or sets the updated name of the school.
    /// </summary>
    /// <value>The new name for the school. This field is required and cannot be empty.</value>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Gets or sets the updated URL to the school's logo image.
    /// </summary>
    /// <value>The new URL pointing to the school's logo image, or null to clear the logo URL.</value>
    public string? LogoURL { get; set; }
}