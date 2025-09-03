using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Api.Functions;

public class SimpleSchoolManagementFunction(ILogger<SimpleSchoolManagementFunction> logger, ICsvImportService csvImportService)
{
    private readonly ILogger<SimpleSchoolManagementFunction> _logger = logger;
    private readonly ICsvImportService _csvImportService = csvImportService;
    
    // In-memory storage for now (this will be lost on restart, but works for testing)
    private static readonly ConcurrentDictionary<int, SchoolData> _schools = new();
    private static int _nextId = 1;
    private static readonly object _idLock = new object();

    [Function("GetSchoolsSimple")]
    public async Task<HttpResponseData> GetSchools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            // TODO: Temporarily disable auth for debugging
            /*
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to get schools");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }
            */
            
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

            // TODO: Temporarily disable auth for debugging
            /*
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to create school");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }
            */

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

    [Function("ImportSchoolsCsvSimple")]
    public async Task<HttpResponseData> ImportSchoolsCsv(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools/import")] HttpRequestData req)
    {
        try
        {
            // TODO: Temporarily disable auth for debugging  
            /*
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to import schools CSV");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }
            */

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
            _logger.LogWarning(ex, "Failed to validate admin token");
        }

        return false;
    }

    private static int GetNextId()
    {
        lock (_idLock)
        {
            return _nextId++;
        }
    }

}

public class SchoolData
{
    public int SchoolId { get; set; }
    public string Name { get; set; } = "";
    public string? LogoURL { get; set; }
    public string? LogoFileName { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}

public class CreateSchoolDto
{
    public string Name { get; set; } = "";
    public string? LogoURL { get; set; }
    public string? LogoFileName { get; set; }
}