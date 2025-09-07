using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Services;
using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Provides HTTP endpoints for managing auctions in the Leagify Fantasy Auction system.
/// This service handles CRUD operations for auctions including creation, retrieval, and status management.
/// All endpoints require valid management authentication tokens.
/// </summary>
/// <remarks>
/// This function provides the core auction management capabilities including join code generation,
/// auction creation, and status transitions. All operations are logged for audit purposes.
/// </remarks>
public class AuctionManagementFunction
{
    private readonly ILogger<AuctionManagementFunction> _logger;
    private readonly IAuctionService _auctionService;

    /// <summary>
    /// Initializes a new instance of the AuctionManagementFunction class.
    /// </summary>
    /// <param name="logger">The logger for function operations.</param>
    /// <param name="auctionService">The auction service for business logic operations.</param>
    public AuctionManagementFunction(ILogger<AuctionManagementFunction> logger, IAuctionService auctionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auctionService = auctionService ?? throw new ArgumentNullException(nameof(auctionService));
    }

    /// <summary>
    /// Creates a new auction with automatically generated join codes.
    /// </summary>
    /// <param name="req">The HTTP request containing the auction creation data and management authentication headers.</param>
    /// <returns>
    /// HTTP 201 Created with the created auction object including generated join codes on success.
    /// HTTP 400 Bad Request if the request body is invalid, empty, or contains invalid JSON.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during creation.
    /// </returns>
    /// <remarks>
    /// Expects a JSON body with Name (required) and CreatedByUserId (required) properties.
    /// Automatically generates unique 6-character join code and 16-character master recovery code.
    /// The created auction starts in "Draft" status and can be configured before being started.
    /// Returns the complete auction object including all generated codes and timestamps.
    /// </remarks>
    [Function("CreateAuction")]
    public async Task<HttpResponseData> CreateAuction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auctions")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("=== CREATE AUCTION REQUEST STARTED ===");

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to create auction");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Request body: {RequestBody}", requestBody);
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("Empty request body received");
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }

            CreateAuctionDto? auctionDto;
            try
            {
                auctionDto = JsonSerializer.Deserialize<CreateAuctionDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON: {RequestBody}", requestBody);
                var jsonErrorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await jsonErrorResponse.WriteStringAsync($"Invalid JSON format: {ex.Message}");
                return jsonErrorResponse;
            }

            if (auctionDto == null || string.IsNullOrWhiteSpace(auctionDto.Name))
            {
                _logger.LogWarning("Invalid auction data - missing name");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body or missing auction name");
                return badResponse;
            }

            // Use nullable CreatedByUserId - will be set properly when user system is implemented  
            int? createdByUserId = auctionDto.CreatedByUserId;
            
            _logger.LogInformation("Creating auction with Name: {Name}, CreatedByUserId: {CreatedByUserId}, Description: {Description}", 
                auctionDto.Name, createdByUserId, auctionDto.Description);

            Auction auction;
            try
            {
                auction = await _auctionService.CreateAuctionAsync(auctionDto.Name, createdByUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CreateAuctionAsync - Name: {Name}, CreatedByUserId: {CreatedByUserId}", 
                    auctionDto.Name, auctionDto.CreatedByUserId);
                throw; // Re-throw to be caught by outer handler
            }

            _logger.LogInformation("Created auction {AuctionId} with join code {JoinCode}", 
                auction.AuctionId, auction.JoinCode);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new AuctionResponseDto
            {
                AuctionId = auction.AuctionId,
                Name = auction.Name,
                JoinCode = auction.JoinCode,
                MasterRecoveryCode = auction.MasterRecoveryCode,
                Status = auction.Status,
                CreatedByUserId = auction.CreatedByUserId,
                CreatedDate = auction.CreatedDate,
                StartedDate = auction.StartedDate,
                CompletedDate = auction.CompletedDate,
                ModifiedDate = auction.ModifiedDate
            });
            
            response.Headers.Add("Location", $"/api/management/auctions/{auction.AuctionId}");
            _logger.LogInformation("=== CREATE AUCTION REQUEST COMPLETED SUCCESSFULLY ===");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== CREATE AUCTION REQUEST FAILED WITH EXCEPTION ===");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error creating auction: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Retrieves an auction by its join code.
    /// </summary>
    /// <param name="req">The HTTP request containing query parameters and authentication headers.</param>
    /// <param name="joinCode">The join code to search for (case-insensitive).</param>
    /// <returns>
    /// HTTP 200 OK with the auction object if found.
    /// HTTP 404 Not Found if no auction exists with the specified join code.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during retrieval.
    /// </returns>
    /// <remarks>
    /// Join code comparison is case-insensitive. Returns the complete auction information
    /// including current status and bidding state if applicable.
    /// </remarks>
    [Function("GetAuctionByJoinCode")]
    public async Task<HttpResponseData> GetAuctionByJoinCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auctions/join/{joinCode}")] HttpRequestData req,
        string joinCode)
    {
        try
        {
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to get auction by join code");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("Looking up auction with join code: {JoinCode}", joinCode);

            var auction = await _auctionService.GetAuctionByJoinCodeAsync(joinCode);
            
            if (auction == null)
            {
                _logger.LogWarning("No auction found with join code: {JoinCode}", joinCode);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new AuctionResponseDto
            {
                AuctionId = auction.AuctionId,
                Name = auction.Name,
                JoinCode = auction.JoinCode,
                MasterRecoveryCode = auction.MasterRecoveryCode,
                Status = auction.Status,
                CreatedByUserId = auction.CreatedByUserId,
                CreatedDate = auction.CreatedDate,
                StartedDate = auction.StartedDate,
                CompletedDate = auction.CompletedDate,
                CurrentNominatorUserId = auction.CurrentNominatorUserId,
                CurrentSchoolId = auction.CurrentSchoolId,
                CurrentHighBid = auction.CurrentHighBid,
                CurrentHighBidderUserId = auction.CurrentHighBidderUserId,
                ModifiedDate = auction.ModifiedDate
            });
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auction by join code: {JoinCode}", joinCode);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error retrieving auction: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Updates the status of an auction.
    /// </summary>
    /// <param name="req">The HTTP request containing the status update data and management authentication headers.</param>
    /// <param name="auctionId">The unique integer ID of the auction to update.</param>
    /// <returns>
    /// HTTP 200 OK with success message if the status was updated.
    /// HTTP 400 Bad Request if the request body is invalid or the status transition is not allowed.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 404 Not Found if the specified auction ID does not exist.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during update.
    /// </returns>
    /// <remarks>
    /// Expects a JSON body with Status property containing one of: "Draft", "InProgress", "Complete", "Archived".
    /// Validates that the status transition is allowed and updates corresponding date fields.
    /// Invalid transitions (e.g., Complete to Draft) will return a 400 Bad Request error.
    /// </remarks>
    [Function("UpdateAuctionStatus")]
    public async Task<HttpResponseData> UpdateAuctionStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "management/auctions/{auctionId:int}/status")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to update auction status");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("Updating status for auction {AuctionId}", auctionId);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var emptyResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await emptyResponse.WriteStringAsync("Empty request body");
                return emptyResponse;
            }

            var statusDto = JsonSerializer.Deserialize<UpdateAuctionStatusDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (statusDto == null || string.IsNullOrWhiteSpace(statusDto.Status))
            {
                var invalidResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await invalidResponse.WriteStringAsync("Invalid status data or missing status");
                return invalidResponse;
            }

            var success = await _auctionService.UpdateAuctionStatusAsync(auctionId, statusDto.Status);
            
            if (!success)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            _logger.LogInformation("Successfully updated auction {AuctionId} status to {Status}", 
                auctionId, statusDto.Status);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Auction status updated successfully", auctionId, status = statusDto.Status });
            return response;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid status value for auction {AuctionId}", auctionId);
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync(ex.Message);
            return badResponse;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid status transition for auction {AuctionId}", auctionId);
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync(ex.Message);
            return badResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auction {AuctionId} status", auctionId);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error updating auction status: {ex.Message}");
            return response;
        }
    }

    /// <summary>
    /// Retrieves all auctions in the system with their basic information.
    /// </summary>
    /// <param name="req">The HTTP request containing management authentication headers.</param>
    /// <returns>
    /// HTTP 200 OK with an array of auction objects containing basic information on success.
    /// HTTP 401 Unauthorized if the management token is invalid or missing.
    /// HTTP 500 Internal Server Error if an unexpected error occurs during retrieval.
    /// </returns>
    /// <remarks>
    /// Returns all auctions with their join codes, master codes, status, and creation information.
    /// Auctions are returned sorted by creation date (most recent first).
    /// This endpoint is used by the management interface to display auction listings.
    /// </remarks>
    /// <summary>
    /// Basic database connectivity test without creating entities.
    /// </summary>
    [Function("TestBasicDb")]
    public async Task<HttpResponseData> TestBasicDb(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/test-basic")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Testing basic database connectivity...");
            
            // Test getting all auctions (read-only)
            var auctions = await _auctionService.GetAllAuctionsAsync();
            _logger.LogInformation("GetAllAuctions successful. Count: {Count}", auctions.Count);
            
            // Test join code validation (doesn't hit database write)
            var (isValid, errorMessage) = await _auctionService.ValidateJoinCodeAsync("TEST99");
            _logger.LogInformation("ValidateJoinCodeAsync successful. IsValid: {IsValid}, Error: {Error}", isValid, errorMessage);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Basic database test successful. Auctions: {auctions.Count}, Join code validation: {isValid}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Basic database test failed: {Message}", ex.Message);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Basic database test failed: {ex.Message}\nStack Trace: {ex.StackTrace}");
            return response;
        }
    }

    /// <summary>
    /// Simple service connectivity test endpoint.
    /// </summary>
    [Function("TestService")]
    public async Task<HttpResponseData> TestService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/test-service")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Testing auction service...");
            
            // Test getting all auctions
            var auctions = await _auctionService.GetAllAuctionsAsync();
            _logger.LogInformation("Service connection successful. Auction count: {Count}", auctions.Count);
            
            // Test creating an auction using the service
            var testName = "Test Auction " + DateTime.UtcNow.Ticks;
            _logger.LogInformation("Creating test auction with name: {Name}", testName);
            
            var testAuction = await _auctionService.CreateAuctionAsync(testName, null);
            
            _logger.LogInformation("Test auction created successfully with ID: {AuctionId}, JoinCode: {JoinCode}", 
                testAuction.AuctionId, testAuction.JoinCode);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Service test successful. Auctions: {auctions.Count}, Test auction ID: {testAuction.AuctionId}, JoinCode: {testAuction.JoinCode}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service test failed: {Message}", ex.Message);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Service test failed: {ex.Message}\nStack Trace: {ex.StackTrace}");
            return response;
        }
    }

    [Function("GetAllAuctions")]
    public async Task<HttpResponseData> GetAllAuctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auctions")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("=== GET ALL AUCTIONS REQUEST STARTED ===");

            // Validate admin token
            if (!IsValidAdminRequest(req))
            {
                _logger.LogWarning("Unauthorized request to get all auctions");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            _logger.LogInformation("Retrieving all auctions");

            var auctions = await _auctionService.GetAllAuctionsAsync();
            
            _logger.LogInformation("Found {AuctionCount} auctions", auctions.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(auctions.OrderByDescending(a => a.CreatedDate).ToList());
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all auctions");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error retrieving auctions: {ex.Message}");
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
    /// Logs a warning message if the token validation fails.
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
}

/// <summary>
/// Data transfer object for creating a new auction.
/// </summary>
/// <remarks>
/// This class is used to deserialize JSON data from HTTP requests when creating new auctions.
/// All validation should be performed on the server side after deserialization.
/// </remarks>
public class CreateAuctionDto
{
    /// <summary>
    /// Gets or sets the name of the auction to be created.
    /// </summary>
    /// <value>The display name for the auction. This field is required and cannot be empty.</value>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the optional description of the auction.
    /// </summary>
    /// <value>A brief description of the auction rules or league details.</value>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the user creating the auction.
    /// </summary>
    /// <value>The user ID of the auction master who will manage the auction.</value>
    public int? CreatedByUserId { get; set; }
}

/// <summary>
/// Data transfer object for updating auction status.
/// </summary>
/// <remarks>
/// This class is used to deserialize JSON data from HTTP requests when updating auction status.
/// The status must be one of the valid status values: Draft, InProgress, Complete, Archived.
/// </remarks>
public class UpdateAuctionStatusDto
{
    /// <summary>
    /// Gets or sets the new status for the auction.
    /// </summary>
    /// <value>The new status value. Must be one of: "Draft", "InProgress", "Complete", "Archived".</value>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for auction responses.
/// </summary>
/// <remarks>
/// This class is used to serialize auction data for HTTP responses.
/// It includes all relevant auction information while excluding sensitive navigation properties.
/// </remarks>
public class AuctionResponseDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the auction.
    /// </summary>
    public int AuctionId { get; set; }
    
    /// <summary>
    /// Gets or sets the display name of the auction.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the join code for the auction.
    /// </summary>
    public string JoinCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the master recovery code for the auction.
    /// </summary>
    public string MasterRecoveryCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the current status of the auction.
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the ID of the user who created the auction.
    /// </summary>
    public int CreatedByUserId { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the auction was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the auction was started.
    /// </summary>
    public DateTime? StartedDate { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the auction was completed.
    /// </summary>
    public DateTime? CompletedDate { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the user whose turn it is to nominate a school.
    /// </summary>
    public int? CurrentNominatorUserId { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the school currently being bid on.
    /// </summary>
    public int? CurrentSchoolId { get; set; }
    
    /// <summary>
    /// Gets or sets the current highest bid amount.
    /// </summary>
    public decimal? CurrentHighBid { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the user who placed the current highest bid.
    /// </summary>
    public int? CurrentHighBidderUserId { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the auction was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; }
}