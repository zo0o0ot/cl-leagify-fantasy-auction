using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Net;
using LeagifyFantasyAuction.Api.Data;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Provides HTTP endpoints for serving school logos with optimal performance strategy.
/// Implements local file-first approach with URL fallback for seamless logo delivery.
/// </summary>
/// <remarks>
/// This service prioritizes locally stored logo files for performance and reliability.
/// When a local file is not available, it falls back to the original URL if provided.
/// This strategy reduces external dependencies and improves load times while maintaining flexibility.
/// </remarks>
public class LogoServingFunction
{
    private readonly ILogger<LogoServingFunction> _logger;
    private readonly LeagifyAuctionDbContext _context;
    private readonly string _logoStoragePath;

    /// <summary>
    /// Initializes a new instance of the LogoServingFunction class.
    /// </summary>
    /// <param name="logger">The logger for function operations.</param>
    /// <param name="context">The database context for school lookup operations.</param>
    public LogoServingFunction(ILogger<LogoServingFunction> logger, LeagifyAuctionDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logoStoragePath = Path.Combine("wwwroot", "images");
    }

    /// <summary>
    /// Serves a school logo using the local-first, URL-fallback strategy.
    /// </summary>
    /// <param name="req">The HTTP request for the logo.</param>
    /// <param name="schoolId">The unique integer ID of the school whose logo is requested.</param>
    /// <returns>
    /// HTTP 200 OK with the logo file content if found locally or via URL.
    /// HTTP 302 Found redirect to the original URL if no local file exists but URL is available.
    /// HTTP 404 Not Found if the school doesn't exist or no logo is available.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during serving.
    /// </returns>
    /// <remarks>
    /// Strategy:
    /// 1. Look up the school in the database to get LogoFileName and LogoURL
    /// 2. If LogoFileName exists and local file is present, serve the local file
    /// 3. If local file doesn't exist but LogoURL is available, redirect to the URL
    /// 4. If neither is available, return 404
    /// 
    /// Local files are served with appropriate content types and caching headers.
    /// This endpoint does not require authentication as logos are public resources.
    /// </remarks>
    [Function("GetSchoolLogo")]
    public async Task<HttpResponseData> GetSchoolLogo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "schools/{schoolId:int}/logo")] HttpRequestData req,
        int schoolId)
    {
        try
        {
            _logger.LogDebug("Logo request for school ID: {SchoolId}", schoolId);

            // Look up school in database
            var school = await _context.Schools
                .Where(s => s.SchoolId == schoolId)
                .Select(s => new { s.SchoolId, s.Name, s.LogoFileName, s.LogoURL })
                .FirstOrDefaultAsync();

            if (school == null)
            {
                _logger.LogWarning("School not found for logo request: ID {SchoolId}", schoolId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("School not found");
                return notFoundResponse;
            }

            // Strategy 1: Try to serve local file first
            if (!string.IsNullOrEmpty(school.LogoFileName))
            {
                var localFilePath = Path.Combine(_logoStoragePath, school.LogoFileName);
                
                if (File.Exists(localFilePath))
                {
                    _logger.LogDebug("Serving local logo file for {SchoolName}: {FileName}", 
                        school.Name, school.LogoFileName);
                    
                    try
                    {
                        var fileBytes = await File.ReadAllBytesAsync(localFilePath);
                        var response = req.CreateResponse(HttpStatusCode.OK);
                        
                        // Set appropriate content type based on file extension
                        var contentType = GetContentType(school.LogoFileName);
                        response.Headers.Add("Content-Type", contentType);
                        
                        // Add caching headers for better performance
                        response.Headers.Add("Cache-Control", "public, max-age=3600"); // Cache for 1 hour
                        response.Headers.Add("ETag", $"\"{school.SchoolId}-{File.GetLastWriteTimeUtc(localFilePath):yyyyMMddHHmmss}\"");
                        
                        await response.WriteBytesAsync(fileBytes);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading local logo file for {SchoolName}: {FileName}", 
                            school.Name, school.LogoFileName);
                        // Fall through to URL fallback
                    }
                }
                else
                {
                    _logger.LogDebug("Local logo file not found for {SchoolName}: {FileName}", 
                        school.Name, school.LogoFileName);
                    // Fall through to URL fallback
                }
            }

            // Strategy 2: Fallback to original URL if available
            if (!string.IsNullOrEmpty(school.LogoURL))
            {
                _logger.LogDebug("Redirecting to original URL for {SchoolName}: {LogoURL}", 
                    school.Name, school.LogoURL);
                
                var redirectResponse = req.CreateResponse(HttpStatusCode.Found);
                redirectResponse.Headers.Add("Location", school.LogoURL);
                redirectResponse.Headers.Add("Cache-Control", "public, max-age=300"); // Cache redirect for 5 minutes
                return redirectResponse;
            }

            // Strategy 3: No logo available
            _logger.LogDebug("No logo available for {SchoolName} (ID: {SchoolId})", school.Name, schoolId);
            var noLogoResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await noLogoResponse.WriteStringAsync("No logo available for this school");
            return noLogoResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving logo for school ID: {SchoolId}", schoolId);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error serving logo: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Serves a school logo by school name using the local-first, URL-fallback strategy.
    /// </summary>
    /// <param name="req">The HTTP request for the logo.</param>
    /// <param name="schoolName">The name of the school whose logo is requested (URL encoded).</param>
    /// <returns>
    /// HTTP 200 OK with the logo file content if found locally or via URL.
    /// HTTP 302 Found redirect to the original URL if no local file exists but URL is available.
    /// HTTP 404 Not Found if the school doesn't exist or no logo is available.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during serving.
    /// </returns>
    /// <remarks>
    /// This is an alternative endpoint that allows lookup by school name instead of ID.
    /// School name comparison is case-insensitive for user convenience.
    /// Uses the same local-first, URL-fallback strategy as the ID-based endpoint.
    /// </remarks>
    [Function("GetSchoolLogoByName")]
    public async Task<HttpResponseData> GetSchoolLogoByName(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "schools/by-name/{schoolName}/logo")] HttpRequestData req,
        string schoolName)
    {
        try
        {
            var decodedName = Uri.UnescapeDataString(schoolName);
            _logger.LogDebug("Logo request for school name: {SchoolName}", decodedName);

            // Look up school by name in database (case-insensitive)
            var school = await _context.Schools
                .Where(s => s.Name.ToLower() == decodedName.ToLower())
                .Select(s => new { s.SchoolId, s.Name, s.LogoFileName, s.LogoURL })
                .FirstOrDefaultAsync();

            if (school == null)
            {
                _logger.LogWarning("School not found for logo request: Name '{SchoolName}'", decodedName);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("School not found");
                return notFoundResponse;
            }

            // Delegate to the ID-based method for consistent behavior
            return await GetSchoolLogoById(req, school.SchoolId, school.Name, school.LogoFileName, school.LogoURL);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving logo for school name: {SchoolName}", schoolName);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error serving logo: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Internal helper method to serve logo by school data.
    /// </summary>
    /// <param name="req">The HTTP request.</param>
    /// <param name="schoolId">The school ID.</param>
    /// <param name="schoolName">The school name.</param>
    /// <param name="logoFileName">The local logo filename.</param>
    /// <param name="logoURL">The logo URL.</param>
    /// <returns>HTTP response with logo or appropriate error.</returns>
    private async Task<HttpResponseData> GetSchoolLogoById(HttpRequestData req, int schoolId, string schoolName, string? logoFileName, string? logoURL)
    {
        // Strategy 1: Try to serve local file first
        if (!string.IsNullOrEmpty(logoFileName))
        {
            var localFilePath = Path.Combine(_logoStoragePath, logoFileName);
            
            if (File.Exists(localFilePath))
            {
                _logger.LogInformation("Serving local logo file for {SchoolName}: {FileName}", 
                    schoolName, logoFileName);
                
                try
                {
                    var fileBytes = await File.ReadAllBytesAsync(localFilePath);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    
                    // Set appropriate content type based on file extension
                    var contentType = GetContentType(logoFileName);
                    response.Headers.Add("Content-Type", contentType);
                    
                    // Add caching headers for better performance
                    response.Headers.Add("Cache-Control", "public, max-age=3600"); // Cache for 1 hour
                    response.Headers.Add("ETag", $"\"{schoolId}-{File.GetLastWriteTimeUtc(localFilePath):yyyyMMddHHmmss}\"");
                    
                    await response.WriteBytesAsync(fileBytes);
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading local logo file for {SchoolName}: {FileName}", 
                        schoolName, logoFileName);
                    // Fall through to URL fallback
                }
            }
        }

        // Strategy 2: Fallback to original URL if available
        if (!string.IsNullOrEmpty(logoURL))
        {
            _logger.LogInformation("Redirecting to original URL for {SchoolName}: {LogoURL}", 
                schoolName, logoURL);
            
            var redirectResponse = req.CreateResponse(HttpStatusCode.Found);
            redirectResponse.Headers.Add("Location", logoURL);
            redirectResponse.Headers.Add("Cache-Control", "public, max-age=300"); // Cache redirect for 5 minutes
            return redirectResponse;
        }

        // Strategy 3: No logo available
        _logger.LogInformation("No logo available for {SchoolName} (ID: {SchoolId})", schoolName, schoolId);
        var noLogoResponse = req.CreateResponse(HttpStatusCode.NotFound);
        await noLogoResponse.WriteStringAsync("No logo available for this school");
        return noLogoResponse;
    }

    /// <summary>
    /// Determines the appropriate content type based on file extension.
    /// </summary>
    /// <param name="fileName">The filename to examine.</param>
    /// <returns>The MIME type string for the file.</returns>
    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        return extension switch
        {
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }
}