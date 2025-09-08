using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Function for handling CSV imports for auction creation.
/// Provides endpoints for CSV preview and confirmed import operations.
/// </summary>
public class AuctionCsvImportFunction
{
    private readonly ILogger<AuctionCsvImportFunction> _logger;
    private readonly IAuctionCsvImportService _auctionCsvImportService;

    /// <summary>
    /// Initializes a new instance of the AuctionCsvImportFunction.
    /// </summary>
    /// <param name="logger">Logger for function execution tracking.</param>
    /// <param name="auctionCsvImportService">Service for auction CSV import operations.</param>
    public AuctionCsvImportFunction(ILogger<AuctionCsvImportFunction> logger,
        IAuctionCsvImportService auctionCsvImportService)
    {
        _logger = logger;
        _auctionCsvImportService = auctionCsvImportService;
    }

    /// <summary>
    /// Previews a CSV upload for an auction without persisting data.
    /// Analyzes school matches and provides confirmation data for the user.
    /// </summary>
    /// <param name="req">HTTP request containing CSV file and auction ID.</param>
    /// <param name="auctionId">The ID of the auction to import schools for.</param>
    /// <returns>Preview results with school matching information.</returns>
    [Function("PreviewAuctionCsv")]
    public async Task<HttpResponseData> PreviewAuctionCsv(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auctions/{auctionId:int}/csv/preview")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("CSV preview request received for auction {AuctionId}", auctionId);

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized CSV preview request for auction {AuctionId}", auctionId);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Parse multipart form data to extract CSV file
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues) ||
                !contentTypeValues.Any(ct => ct.StartsWith("multipart/form-data")))
            {
                _logger.LogWarning("Invalid content type for CSV upload for auction {AuctionId}", auctionId);
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Expected multipart/form-data content type");
                return badResponse;
            }

            Stream? csvStream = null;
            try
            {
                // For now, we'll read the raw body and extract CSV content manually
                // This is a simplified approach - in production you might want to use a proper multipart parser
                using var bodyReader = new StreamReader(req.Body);
                var bodyContent = await bodyReader.ReadToEndAsync();
                
                _logger.LogInformation("Raw multipart body length: {Length}", bodyContent.Length);
                _logger.LogInformation("First 500 chars of body: {Body}", 
                    bodyContent.Length > 500 ? bodyContent.Substring(0, 500) : bodyContent);
                
                // Find the CSV content between multipart boundaries
                var lines = bodyContent.Split('\n');
                var csvStartIndex = -1;
                var csvEndIndex = -1;
                bool foundContentType = false;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    _logger.LogInformation("Line {Index}: '{Line}'", i, line.Length > 100 ? line.Substring(0, 100) + "..." : line);
                    
                    if (line.Contains("Content-Type:") && (line.Contains("text/csv") || line.Contains("application/")))
                    {
                        foundContentType = true;
                        _logger.LogInformation("Found content type at line {Index}", i);
                    }
                    else if (foundContentType && string.IsNullOrEmpty(line))
                    {
                        // CSV content starts after the empty line following Content-Type
                        csvStartIndex = i + 1;
                        _logger.LogInformation("CSV content starts at line {Index}", csvStartIndex);
                    }
                    else if (csvStartIndex > 0 && line.StartsWith("--"))
                    {
                        // End boundary found
                        csvEndIndex = i;
                        _logger.LogInformation("End boundary found at line {Index}", i);
                        break;
                    }
                }

                if (csvStartIndex < 0)
                {
                    _logger.LogWarning("Could not find CSV content start in multipart data for auction {AuctionId}. Lines count: {Count}", 
                        auctionId, lines.Length);
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync($"CSV content not found in upload. Found {lines.Length} lines, Content-Type found: {foundContentType}");
                    return badResponse;
                }

                // If no end boundary found, use the rest of the content
                if (csvEndIndex < 0)
                {
                    csvEndIndex = lines.Length;
                    _logger.LogInformation("No end boundary found, using all remaining lines until {Index}", csvEndIndex);
                }

                // Extract CSV content
                var csvLines = lines.Skip(csvStartIndex).Take(csvEndIndex - csvStartIndex)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("--"))
                    .ToArray();
                var csvContent = string.Join("\n", csvLines).Trim();
                
                _logger.LogInformation("Extracted CSV content length: {Length}, First 200 chars: {Content}", 
                    csvContent.Length, csvContent.Length > 200 ? csvContent.Substring(0, 200) : csvContent);
                
                if (string.IsNullOrEmpty(csvContent))
                {
                    _logger.LogWarning("Empty CSV content extracted for auction {AuctionId}", auctionId);
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("CSV file is empty");
                    return badResponse;
                }

                csvStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing multipart form data for auction {AuctionId}", auctionId);
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Error parsing uploaded file: {ex.Message}");
                return errorResponse;
            }

            if (csvStream == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("No CSV content found");
                return badResponse;
            }

            // Generate preview
            using (csvStream)
            {
                var previewResult = await _auctionCsvImportService.PreviewCsvImportAsync(csvStream, auctionId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(previewResult);

                _logger.LogInformation("CSV preview completed for auction {AuctionId}. Success: {Success}, Schools: {Count}",
                    auctionId, previewResult.IsSuccess, previewResult.TotalSchools);

                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV preview for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"CSV preview failed: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Completes the CSV import after user confirmation of school matches.
    /// Creates AuctionSchool entities and persists them to the database.
    /// </summary>
    /// <param name="req">HTTP request containing confirmed school matches.</param>
    /// <param name="auctionId">The ID of the auction to import schools for.</param>
    /// <returns>Import completion results.</returns>
    [Function("ConfirmAuctionCsvImport")]
    public async Task<HttpResponseData> ConfirmAuctionCsvImport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auctions/{auctionId:int}/csv/confirm")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("CSV import confirmation request received for auction {AuctionId}", auctionId);

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized CSV import confirmation for auction {AuctionId}", auctionId);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Parse confirmed matches from request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }

            List<ConfirmedSchoolMatch>? confirmedMatches;
            try
            {
                confirmedMatches = JsonSerializer.Deserialize<List<ConfirmedSchoolMatch>>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize confirmed matches for auction {AuctionId}", auctionId);
                var jsonErrorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await jsonErrorResponse.WriteStringAsync($"Invalid JSON format: {ex.Message}");
                return jsonErrorResponse;
            }

            if (confirmedMatches == null || !confirmedMatches.Any())
            {
                var noDataResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await noDataResponse.WriteStringAsync("No confirmed matches provided");
                return noDataResponse;
            }

            // Complete the import
            var importResult = await _auctionCsvImportService.CompleteCsvImportAsync(auctionId, confirmedMatches);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(importResult);

            _logger.LogInformation("CSV import completed for auction {AuctionId}. Success: {Success}, Schools: {Count}",
                auctionId, importResult.IsSuccess, importResult.TotalSchools);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV import confirmation for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"CSV import failed: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Test endpoint to verify multipart form data handling without processing
    /// </summary>
    [Function("TestCsvUpload")]
    public async Task<HttpResponseData> TestCsvUpload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "test/csv-upload")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Test CSV upload request received");

            var response = req.CreateResponse(HttpStatusCode.OK);
            
            // Log all headers
            foreach (var header in req.Headers)
            {
                _logger.LogInformation("Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
            }

            // Read and log body content
            using var bodyReader = new StreamReader(req.Body);
            var bodyContent = await bodyReader.ReadToEndAsync();
            
            var testResult = new
            {
                Message = "Test endpoint working",
                ContentLength = bodyContent.Length,
                ContentType = req.Headers.TryGetValues("Content-Type", out var ctValues) ? string.Join(", ", ctValues) : "None",
                BodyPreview = bodyContent.Length > 200 ? bodyContent.Substring(0, 200) + "..." : bodyContent,
                HasMultipartBoundary = bodyContent.Contains("Content-Disposition"),
                HasCsvContent = bodyContent.ToLower().Contains("school") || bodyContent.ToLower().Contains("conference")
            };

            _logger.LogInformation("Test result: {@TestResult}", testResult);
            
            await response.WriteAsJsonAsync(testResult);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test CSV upload");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Test failed: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Gets the current school list for an auction.
    /// Useful for previewing what schools are already imported.
    /// </summary>
    /// <param name="req">HTTP request.</param>
    /// <param name="auctionId">The ID of the auction.</param>
    /// <returns>List of schools currently associated with the auction.</returns>
    [Function("GetAuctionSchools")]
    public async Task<HttpResponseData> GetAuctionSchools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auctions/{auctionId:int}/schools")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Get auction schools request received for auction {AuctionId}", auctionId);

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized get auction schools request for auction {AuctionId}", auctionId);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // This would be implemented with a service method
            // For now, return a placeholder response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Feature coming soon", auctionId });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auction schools for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Failed to get auction schools: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Validates management token for admin requests.
    /// </summary>
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