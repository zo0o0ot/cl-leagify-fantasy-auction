using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace LeagifyFantasyAuction.Api.Functions;

public class SchoolManagementFunction(ILogger<SchoolManagementFunction> logger, LeagifyAuctionDbContext dbContext)
{
    private readonly ILogger<SchoolManagementFunction> _logger = logger;
    private readonly LeagifyAuctionDbContext _dbContext = dbContext;

    [Function("GetSchools")]
    public async Task<HttpResponseData> GetSchools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            // TODO: Add token validation
            
            var schools = await _dbContext.Schools
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

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(schools);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schools");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Error retrieving schools");
            return response;
        }
    }

    [Function("GetSchool")]
    public async Task<HttpResponseData> GetSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/schools/{id:int}")] HttpRequestData req,
        int id)
    {
        try
        {
            // TODO: Add token validation

            var school = await _dbContext.Schools
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
                .FirstOrDefaultAsync(s => s.SchoolId == id);

            if (school == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(school);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving school {SchoolId}", id);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Error retrieving school");
            return response;
        }
    }

    [Function("CreateSchool")]
    public async Task<HttpResponseData> CreateSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools")] HttpRequestData req)
    {
        try
        {
            // TODO: Add token validation

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var schoolDto = JsonSerializer.Deserialize<CreateSchoolDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (schoolDto == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body");
                return badResponse;
            }

            // Validate
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(schoolDto);
            if (!Validator.TryValidateObject(schoolDto, validationContext, validationResults, true))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(validationResults.Select(vr => vr.ErrorMessage));
                return badResponse;
            }

            // Check for duplicate name
            if (await _dbContext.Schools.AnyAsync(s => s.Name == schoolDto.Name))
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("School name already exists");
                return conflictResponse;
            }

            var school = new School
            {
                Name = schoolDto.Name,
                LogoURL = schoolDto.LogoURL,
                LogoFileName = schoolDto.LogoFileName,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _dbContext.Schools.Add(school);
            await _dbContext.SaveChangesAsync();

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
            await response.WriteStringAsync("Error creating school");
            return response;
        }
    }

    [Function("UpdateSchool")]
    public async Task<HttpResponseData> UpdateSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "management/schools/{id:int}")] HttpRequestData req,
        int id)
    {
        try
        {
            // TODO: Add token validation

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var schoolDto = JsonSerializer.Deserialize<UpdateSchoolDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (schoolDto == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body");
                return badResponse;
            }

            // Validate
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(schoolDto);
            if (!Validator.TryValidateObject(schoolDto, validationContext, validationResults, true))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(validationResults.Select(vr => vr.ErrorMessage));
                return badResponse;
            }

            var school = await _dbContext.Schools.FindAsync(id);
            if (school == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            // Check for duplicate name (excluding current school)
            if (await _dbContext.Schools.AnyAsync(s => s.Name == schoolDto.Name && s.SchoolId != id))
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("School name already exists");
                return conflictResponse;
            }

            school.Name = schoolDto.Name;
            school.LogoURL = schoolDto.LogoURL;
            school.LogoFileName = schoolDto.LogoFileName;
            school.ModifiedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated school {SchoolId}: {SchoolName}", school.SchoolId, school.Name);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                school.SchoolId,
                school.Name,
                school.LogoURL,
                school.LogoFileName,
                school.CreatedDate,
                school.ModifiedDate
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating school {SchoolId}", id);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Error updating school");
            return response;
        }
    }

    [Function("DeleteSchool")]
    public async Task<HttpResponseData> DeleteSchool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "management/schools/{id:int}")] HttpRequestData req,
        int id)
    {
        try
        {
            // TODO: Add token validation

            var school = await _dbContext.Schools
                .Include(s => s.AuctionSchools)
                .FirstOrDefaultAsync(s => s.SchoolId == id);

            if (school == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            // Check if school is used in any auctions
            if (school.AuctionSchools.Any())
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("Cannot delete school that is used in auctions");
                return conflictResponse;
            }

            _dbContext.Schools.Remove(school);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted school {SchoolId}: {SchoolName}", school.SchoolId, school.Name);

            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting school {SchoolId}", id);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Error deleting school");
            return response;
        }
    }

    [Function("TestSchoolLogo")]
    public async Task<HttpResponseData> TestSchoolLogo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools/test-logo")] HttpRequestData req)
    {
        try
        {
            // TODO: Add token validation

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<TestLogoDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request?.LogoURL == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("LogoURL is required");
                return badResponse;
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var httpResponse = await httpClient.GetAsync(request.LogoURL);
                var isValid = httpResponse.IsSuccessStatusCode;
                var contentType = httpResponse.Content.Headers.ContentType?.MediaType;

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    IsValid = isValid,
                    StatusCode = (int)httpResponse.StatusCode,
                    ContentType = contentType,
                    Message = isValid ? "Logo URL is accessible" : $"Logo URL returned {httpResponse.StatusCode}"
                });
                return response;
            }
            catch (HttpRequestException ex)
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    IsValid = false,
                    StatusCode = 0,
                    ContentType = (string?)null,
                    Message = $"Request failed: {ex.Message}"
                });
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing logo URL");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Error testing logo URL");
            return response;
        }
    }
}

public class CreateSchoolDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? LogoURL { get; set; }

    [MaxLength(100)]
    public string? LogoFileName { get; set; }
}

public class UpdateSchoolDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? LogoURL { get; set; }

    [MaxLength(100)]
    public string? LogoFileName { get; set; }
}

public class TestLogoDto
{
    [Required]
    public string LogoURL { get; set; } = "";
}