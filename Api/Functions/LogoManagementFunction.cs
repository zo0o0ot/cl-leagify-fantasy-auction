using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using System.Net;
using System.Text.Json;
using System.IO.Compression;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Functions for managing school logos.
/// Handles logo testing, validation, upload, and fallback strategies.
/// </summary>
public class LogoManagementFunction(LeagifyAuctionDbContext context, ILogger<LogoManagementFunction> logger)
{
    private readonly LeagifyAuctionDbContext _context = context;
    private readonly ILogger<LogoManagementFunction> _logger = logger;
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Tests a logo URL to verify it's accessible and returns image metadata.
    /// Provides preview information without permanently storing the logo.
    /// </summary>
    [Function("TestLogoUrl")]
    public async Task<HttpResponseData> TestLogoUrl(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools/test-logo")] HttpRequestData req)
    {
        _logger.LogInformation("TestLogoUrl called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var requestBody = await req.ReadAsStringAsync();
            var testRequest = JsonSerializer.Deserialize<TestLogoRequest>(requestBody ?? string.Empty);

            if (testRequest == null || string.IsNullOrEmpty(testRequest.LogoUrl))
            {
                return await CreateBadRequestResponse(req, "Logo URL is required");
            }

            // Validate URL format
            if (!Uri.TryCreate(testRequest.LogoUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return await CreateBadRequestResponse(req, "Invalid URL format");
            }

            // Test URL accessibility
            var testResult = await TestLogoUrlInternal(testRequest.LogoUrl);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(testResult);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing logo URL");
            return await CreateErrorResponse(req, "Failed to test logo URL");
        }
    }

    /// <summary>
    /// Uploads a logo file for a specific school.
    /// Stores the logo locally and updates the school record.
    /// </summary>
    [Function("UploadLogo")]
    public async Task<HttpResponseData> UploadLogo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools/{schoolId}/upload-logo")] HttpRequestData req,
        int schoolId)
    {
        _logger.LogInformation("UploadLogo called for school {SchoolId}", schoolId);

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            // Get school
            var school = await _context.Schools.FindAsync(schoolId);
            if (school == null)
            {
                return await CreateNotFoundResponse(req, "School not found");
            }

            // Parse multipart form data
            var formData = await ParseMultipartFormData(req);
            if (formData.FileData == null || formData.FileData.Length == 0)
            {
                return await CreateBadRequestResponse(req, "No file data provided");
            }

            // Validate image type
            var extension = GetImageExtension(formData.FileData);
            if (extension == null)
            {
                return await CreateBadRequestResponse(req, "Invalid image format. Supported formats: PNG, JPG, GIF, SVG");
            }

            // Generate safe filename
            var safeSchoolName = string.Concat(school.Name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            var fileName = $"{safeSchoolName}_{school.SchoolId}{extension}";

            // Save logo file (in production, this would save to Azure Blob Storage or similar)
            var logoPath = Path.Combine("wwwroot", "logos", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(logoPath)!);
            await File.WriteAllBytesAsync(logoPath, formData.FileData);

            // Update school record
            school.LogoFileName = fileName;
            school.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Uploaded logo for school {SchoolId}: {FileName}", schoolId, fileName);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Success = true,
                FileName = fileName,
                FilePath = $"/logos/{fileName}",
                Message = "Logo uploaded successfully"
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading logo for school {SchoolId}", schoolId);
            return await CreateErrorResponse(req, "Failed to upload logo");
        }
    }

    /// <summary>
    /// Uploads multiple logos via a ZIP file.
    /// Matches logo files to schools by name and updates records.
    /// </summary>
    [Function("BulkUploadLogos")]
    public async Task<HttpResponseData> BulkUploadLogos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/schools/bulk-upload-logos")] HttpRequestData req)
    {
        _logger.LogInformation("BulkUploadLogos called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            // Parse multipart form data
            var formData = await ParseMultipartFormData(req);
            if (formData.FileData == null || formData.FileData.Length == 0)
            {
                return await CreateBadRequestResponse(req, "No ZIP file provided");
            }

            // Verify it's a ZIP file
            if (!IsZipFile(formData.FileData))
            {
                return await CreateBadRequestResponse(req, "File must be a ZIP archive");
            }

            var results = new List<BulkUploadResult>();
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                // Extract ZIP file
                using (var memoryStream = new MemoryStream(formData.FileData))
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name) || entry.Name.StartsWith("."))
                            continue;

                        var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif" && extension != ".svg")
                            continue;

                        // Extract school name from filename
                        var schoolName = Path.GetFileNameWithoutExtension(entry.Name);

                        // Find matching school
                        var school = await _context.Schools
                            .FirstOrDefaultAsync(s => EF.Functions.Like(s.Name, $"%{schoolName}%"));

                        if (school == null)
                        {
                            results.Add(new BulkUploadResult
                            {
                                FileName = entry.Name,
                                Success = false,
                                Message = "No matching school found"
                            });
                            continue;
                        }

                        // Read file data
                        using var entryStream = entry.Open();
                        using var memStream = new MemoryStream();
                        await entryStream.CopyToAsync(memStream);
                        var fileData = memStream.ToArray();

                        // Generate safe filename
                        var safeSchoolName = string.Concat(school.Name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
                        var fileName = $"{safeSchoolName}_{school.SchoolId}{extension}";

                        // Save logo file
                        var logoPath = Path.Combine("wwwroot", "logos", fileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(logoPath)!);
                        await File.WriteAllBytesAsync(logoPath, fileData);

                        // Update school record
                        school.LogoFileName = fileName;
                        school.ModifiedDate = DateTime.UtcNow;

                        results.Add(new BulkUploadResult
                        {
                            FileName = entry.Name,
                            SchoolId = school.SchoolId,
                            SchoolName = school.Name,
                            Success = true,
                            Message = "Logo uploaded successfully"
                        });
                    }
                }

                await _context.SaveChangesAsync();

                var successCount = results.Count(r => r.Success);
                _logger.LogInformation("Bulk upload completed: {SuccessCount} of {TotalCount} logos uploaded",
                    successCount, results.Count);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    TotalFiles = results.Count,
                    SuccessCount = successCount,
                    FailedCount = results.Count - successCount,
                    Results = results
                });
                return response;
            }
            finally
            {
                // Cleanup temp directory
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk logo upload");
            return await CreateErrorResponse(req, "Failed to process bulk logo upload");
        }
    }

    /// <summary>
    /// Gets logo statistics and availability tracking.
    /// Returns counts of schools with/without logos and identifies schools needing logos.
    /// </summary>
    [Function("GetLogoStatistics")]
    public async Task<HttpResponseData> GetLogoStatistics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/schools/logo-statistics")] HttpRequestData req)
    {
        _logger.LogInformation("GetLogoStatistics called");

        try
        {
            // Verify management authentication
            if (!await VerifyManagementToken(req))
            {
                return await CreateUnauthorizedResponse(req);
            }

            var totalSchools = await _context.Schools.CountAsync();
            var schoolsWithLogoUrl = await _context.Schools.CountAsync(s => !string.IsNullOrEmpty(s.LogoURL));
            var schoolsWithLogoFile = await _context.Schools.CountAsync(s => !string.IsNullOrEmpty(s.LogoFileName));
            var schoolsWithoutLogo = await _context.Schools.CountAsync(s => string.IsNullOrEmpty(s.LogoURL) && string.IsNullOrEmpty(s.LogoFileName));

            // Get schools without logos
            var schoolsNeedingLogos = await _context.Schools
                .Where(s => string.IsNullOrEmpty(s.LogoURL) && string.IsNullOrEmpty(s.LogoFileName))
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.SchoolId,
                    s.Name,
                    AuctionCount = s.AuctionSchools.Count()
                })
                .Take(50)
                .ToListAsync();

            // Test logo URL availability for schools with URLs
            var urlTestResults = new List<LogoUrlStatus>();
            var schoolsWithUrls = await _context.Schools
                .Where(s => !string.IsNullOrEmpty(s.LogoURL))
                .Take(20) // Test first 20 to avoid long delays
                .ToListAsync();

            foreach (var school in schoolsWithUrls)
            {
                var testResult = await TestLogoUrlInternal(school.LogoURL!);
                urlTestResults.Add(new LogoUrlStatus
                {
                    SchoolId = school.SchoolId,
                    SchoolName = school.Name,
                    LogoUrl = school.LogoURL,
                    IsAccessible = testResult.IsAccessible,
                    ErrorMessage = testResult.ErrorMessage
                });
            }

            var statistics = new
            {
                TotalSchools = totalSchools,
                SchoolsWithLogoUrl = schoolsWithLogoUrl,
                SchoolsWithLogoFile = schoolsWithLogoFile,
                SchoolsWithoutLogo = schoolsWithoutLogo,
                LogoCoveragePercentage = totalSchools > 0 
                    ? Math.Round((decimal)(totalSchools - schoolsWithoutLogo) / totalSchools * 100, 2) 
                    : 0,
                SchoolsNeedingLogos = schoolsNeedingLogos,
                UrlTestResults = urlTestResults
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(statistics);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logo statistics");
            return await CreateErrorResponse(req, "Failed to retrieve logo statistics");
        }
    }

    // Helper methods

    private async Task<LogoTestResult> TestLogoUrlInternal(string logoUrl)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, logoUrl);
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return new LogoTestResult
                {
                    IsAccessible = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
            var contentLength = response.Content.Headers.ContentLength ?? 0;

            // Validate it's an image
            if (!contentType.StartsWith("image/"))
            {
                return new LogoTestResult
                {
                    IsAccessible = false,
                    ErrorMessage = $"URL does not point to an image (Content-Type: {contentType})"
                };
            }

            return new LogoTestResult
            {
                IsAccessible = true,
                ContentType = contentType,
                ContentLength = contentLength,
                Message = "Logo URL is accessible"
            };
        }
        catch (HttpRequestException ex)
        {
            return new LogoTestResult
            {
                IsAccessible = false,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            return new LogoTestResult
            {
                IsAccessible = false,
                ErrorMessage = "Request timeout (10 seconds exceeded)"
            };
        }
        catch (Exception ex)
        {
            return new LogoTestResult
            {
                IsAccessible = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    private async Task<bool> VerifyManagementToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Management-Token", out var values))
        {
            var token = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                var validToken = Environment.GetEnvironmentVariable("MANAGEMENT_TOKEN");
                return token == validToken;
            }
        }
        return false;
    }

    private static async Task<MultipartFormData> ParseMultipartFormData(HttpRequestData req)
    {
        var boundary = GetBoundary(req.Headers.GetValues("Content-Type").First());
        if (string.IsNullOrEmpty(boundary))
        {
            throw new InvalidOperationException("Invalid multipart form data");
        }

        using var memoryStream = new MemoryStream();
        await req.Body.CopyToAsync(memoryStream);
        var fileData = memoryStream.ToArray();

        return new MultipartFormData
        {
            FileData = fileData,
            FileName = "uploaded_file"
        };
    }

    private static string? GetBoundary(string contentType)
    {
        var elements = contentType.Split(' ');
        var element = elements.FirstOrDefault(e => e.StartsWith("boundary="));
        if (element == null) return null;

        var boundary = element.Substring("boundary=".Length);
        return boundary.Trim('"');
    }

    private static string? GetImageExtension(byte[] fileData)
    {
        if (fileData.Length < 4) return null;

        // PNG: 89 50 4E 47
        if (fileData[0] == 0x89 && fileData[1] == 0x50 && fileData[2] == 0x4E && fileData[3] == 0x47)
            return ".png";

        // JPEG: FF D8 FF
        if (fileData[0] == 0xFF && fileData[1] == 0xD8 && fileData[2] == 0xFF)
            return ".jpg";

        // GIF: 47 49 46
        if (fileData[0] == 0x47 && fileData[1] == 0x49 && fileData[2] == 0x46)
            return ".gif";

        // SVG check (simple text-based check)
        var text = System.Text.Encoding.UTF8.GetString(fileData.Take(100).ToArray());
        if (text.Contains("<svg"))
            return ".svg";

        return null;
    }

    private static bool IsZipFile(byte[] fileData)
    {
        if (fileData.Length < 4) return false;

        // ZIP: 50 4B 03 04 or 50 4B 05 06
        return (fileData[0] == 0x50 && fileData[1] == 0x4B && 
                (fileData[2] == 0x03 || fileData[2] == 0x05));
    }

    private static async Task<HttpResponseData> CreateUnauthorizedResponse(HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        await response.WriteStringAsync("Unauthorized");
        return response;
    }

    private static async Task<HttpResponseData> CreateNotFoundResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.NotFound);
        await response.WriteStringAsync(message);
        return response;
    }

    private static async Task<HttpResponseData> CreateBadRequestResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteStringAsync(message);
        return response;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteStringAsync(message);
        return response;
    }
}

// Request/Response DTOs

public class TestLogoRequest
{
    public string LogoUrl { get; set; } = string.Empty;
}

public class LogoTestResult
{
    public bool IsAccessible { get; set; }
    public string? ContentType { get; set; }
    public long ContentLength { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BulkUploadResult
{
    public string FileName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class LogoUrlStatus
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsAccessible { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MultipartFormData
{
    public byte[]? FileData { get; set; }
    public string FileName { get; set; } = string.Empty;
}
