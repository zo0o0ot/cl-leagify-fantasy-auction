using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Collections.Concurrent;

namespace LeagifyFantasyAuction.Api.Functions;

public class SchoolCreationTestFunction(ILogger<SchoolCreationTestFunction> logger)
{
    private readonly ILogger<SchoolCreationTestFunction> _logger = logger;

    [Function("TestSchoolCreation")]
    public async Task<HttpResponseData> TestSchoolCreation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "test/school")] HttpRequestData req)
    {
        _logger.LogInformation("=== SCHOOL CREATION TEST STARTED ===");

        try
        {
            // Log request details
            _logger.LogInformation($"Request method: {req.Method}");
            _logger.LogInformation($"Request URL: {req.Url}");
            _logger.LogInformation($"Headers: {string.Join(", ", req.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

            // Read and log request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"Request body: '{requestBody}'");
            _logger.LogInformation($"Request body length: {requestBody.Length}");

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("DIAGNOSTIC: Empty request body received");
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }

            // Test JSON deserialization
            TestSchoolDto? schoolDto;
            try
            {
                _logger.LogInformation("DIAGNOSTIC: Attempting JSON deserialization");
                schoolDto = JsonSerializer.Deserialize<TestSchoolDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation($"DIAGNOSTIC: JSON deserialization successful. Name: '{schoolDto?.Name}', LogoURL: '{schoolDto?.LogoURL}', LogoFileName: '{schoolDto?.LogoFileName}'");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "DIAGNOSTIC: Failed to deserialize JSON: {RequestBody}", requestBody);
                var jsonErrorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await jsonErrorResponse.WriteStringAsync($"Invalid JSON format: {ex.Message}");
                return jsonErrorResponse;
            }

            if (schoolDto == null || string.IsNullOrEmpty(schoolDto.Name))
            {
                _logger.LogWarning("DIAGNOSTIC: Invalid school data received");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body or missing school name");
                return badResponse;
            }

            // Test object creation
            _logger.LogInformation("DIAGNOSTIC: Creating test school object");
            var testSchool = new
            {
                SchoolId = Random.Shared.Next(1000, 9999),
                Name = schoolDto.Name,
                LogoURL = schoolDto.LogoURL,
                LogoFileName = schoolDto.LogoFileName,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                TestStatus = "SUCCESS"
            };

            _logger.LogInformation($"DIAGNOSTIC: School object created successfully: {JsonSerializer.Serialize(testSchool)}");

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(testSchool);
            response.Headers.Add("Location", $"/api/test/school/{testSchool.SchoolId}");
            
            _logger.LogInformation("=== SCHOOL CREATION TEST COMPLETED SUCCESSFULLY ===");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== SCHOOL CREATION TEST FAILED ===");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Test failed: {ex.Message}");
            return response;
        }
    }

    [Function("TestBasicConnectivity")]
    public async Task<HttpResponseData> TestBasicConnectivity(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test/connectivity")] HttpRequestData req)
    {
        _logger.LogInformation("Basic connectivity test");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            Status = "OK",
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Unknown",
            Message = "API is reachable and responding"
        });
        return response;
    }
}

public class TestSchoolDto
{
    public string Name { get; set; } = "";
    public string? LogoURL { get; set; }
    public string? LogoFileName { get; set; }
}