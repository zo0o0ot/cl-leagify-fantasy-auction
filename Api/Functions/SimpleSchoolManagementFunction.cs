using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;

namespace LeagifyFantasyAuction.Api.Functions;

public class SimpleSchoolManagementFunction(ILogger<SimpleSchoolManagementFunction> logger)
{
    private readonly ILogger<SimpleSchoolManagementFunction> _logger = logger;
    
    // In-memory storage for now (this will be lost on restart, but works for testing)
    private static readonly ConcurrentDictionary<int, SchoolData> _schools = new();
    private static int _nextId = 1;

    [Function("GetSchoolsSimple")]
    public async Task<HttpResponseData> GetSchools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            // TODO: Add token validation
            
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
            // TODO: Add token validation

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"Received request body: {requestBody}");
            
            var schoolDto = JsonSerializer.Deserialize<CreateSchoolDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (schoolDto == null || string.IsNullOrEmpty(schoolDto.Name))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body or missing school name");
                return badResponse;
            }

            // Check for duplicate name
            if (_schools.Values.Any(s => s.Name.Equals(schoolDto.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("School name already exists");
                return conflictResponse;
            }

            var school = new SchoolData
            {
                SchoolId = _nextId++,
                Name = schoolDto.Name,
                LogoURL = schoolDto.LogoURL, // Use whatever URL user provided
                LogoFileName = schoolDto.LogoFileName,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _schools.TryAdd(school.SchoolId, school);

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
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating school");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error creating school: {ex.Message}");
            return response;
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