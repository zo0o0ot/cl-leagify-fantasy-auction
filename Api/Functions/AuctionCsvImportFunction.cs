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

            // Get CSV file from the request
            using var csvStream = new MemoryStream();
            await req.Body.CopyToAsync(csvStream);
            csvStream.Position = 0;

            if (csvStream.Length == 0)
            {
                _logger.LogWarning("Empty CSV file received for auction {AuctionId}", auctionId);
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("CSV file is empty");
                return badResponse;
            }

            // Generate preview
            var previewResult = await _auctionCsvImportService.PreviewCsvImportAsync(csvStream, auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(previewResult);

            _logger.LogInformation("CSV preview completed for auction {AuctionId}. Success: {Success}, Schools: {Count}",
                auctionId, previewResult.IsSuccess, previewResult.TotalSchools);

            return response;
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