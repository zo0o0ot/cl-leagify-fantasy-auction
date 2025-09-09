using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Function for managing roster positions in auctions.
/// Handles CRUD operations for roster position configuration.
/// </summary>
/// <remarks>
/// Provides endpoints for auction masters to define team structure with position slots,
/// colors, and display ordering. All endpoints require management authentication.
/// </remarks>
public class RosterPositionFunction(ILogger<RosterPositionFunction> logger, 
                                   LeagifyAuctionDbContext context)
{
    private readonly ILogger<RosterPositionFunction> _logger = logger;
    private readonly LeagifyAuctionDbContext _context = context;

    /// <summary>
    /// Retrieves available LeagifyPosition values from imported schools for dropdown options.
    /// </summary>
    /// <param name="req">The HTTP request containing auction ID in route.</param>
    /// <param name="auctionId">The auction ID from the route parameters.</param>
    /// <returns>JSON array of distinct LeagifyPosition values from imported schools.</returns>
    [Function("GetAvailableLeagifyPositions")]
    public async Task<HttpResponseData> GetAvailableLeagifyPositions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auctions/{auctionId:int}/available-positions")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("Getting available LeagifyPosition values for auction {AuctionId}", auctionId);

        try
        {
            // Authenticate request
            if (!IsValidAdminRequest(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Verify auction exists
            var auctionExists = await _context.Auctions.AnyAsync(a => a.AuctionId == auctionId);
            if (!auctionExists)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            // Get distinct LeagifyPosition values from auction schools
            var availablePositions = await _context.AuctionSchools
                .Where(aas => aas.AuctionId == auctionId)
                .Select(aas => aas.LeagifyPosition)
                .Distinct()
                .Where(pos => !string.IsNullOrEmpty(pos))
                .OrderBy(pos => pos)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(availablePositions));

            _logger.LogInformation("Successfully retrieved {Count} available LeagifyPositions for auction {AuctionId}", availablePositions.Count, auctionId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available LeagifyPositions for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error retrieving available positions: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Retrieves all roster positions for a specific auction.
    /// </summary>
    /// <param name="req">The HTTP request containing auction ID in route.</param>
    /// <param name="auctionId">The auction ID from the route parameters.</param>
    /// <returns>JSON array of roster positions ordered by DisplayOrder.</returns>
    [Function("GetAuctionRosterPositions")]
    public async Task<HttpResponseData> GetAuctionRosterPositions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auctions/{auctionId:int}/roster-positions")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("Getting roster positions for auction {AuctionId}", auctionId);

        try
        {
            // Authenticate request
            if (!IsValidAdminRequest(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Verify auction exists
            var auctionExists = await _context.Auctions.AnyAsync(a => a.AuctionId == auctionId);
            if (!auctionExists)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            // Get roster positions ordered by display order
            var positions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .OrderBy(rp => rp.DisplayOrder)
                .Select(rp => new
                {
                    rp.RosterPositionId,
                    rp.AuctionId,
                    rp.PositionName,
                    rp.SlotsPerTeam,
                    rp.ColorCode,
                    rp.DisplayOrder,
                    rp.IsFlexPosition
                })
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(positions));

            _logger.LogInformation("Successfully retrieved {Count} roster positions for auction {AuctionId}", positions.Count, auctionId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roster positions for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error retrieving roster positions: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Creates a new roster position for an auction.
    /// </summary>
    /// <param name="req">The HTTP request containing position data in JSON body.</param>
    /// <returns>JSON object of the created roster position.</returns>
    [Function("CreateRosterPosition")]
    public async Task<HttpResponseData> CreateRosterPosition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/roster-positions")] HttpRequestData req)
    {
        _logger.LogInformation("Creating new roster position");

        try
        {
            // Authenticate request
            if (!IsValidAdminRequest(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var createRequest = JsonSerializer.Deserialize<CreateRosterPositionRequest>(requestBody);

            if (createRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(createRequest.PositionName))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Position name is required");
                return badRequestResponse;
            }

            if (createRequest.SlotsPerTeam <= 0)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Slots per team must be greater than 0");
                return badRequestResponse;
            }

            // Verify auction exists
            var auctionExists = await _context.Auctions.AnyAsync(a => a.AuctionId == createRequest.AuctionId);
            if (!auctionExists)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            // Check for duplicate position name within the auction
            var duplicateExists = await _context.RosterPositions
                .AnyAsync(rp => rp.AuctionId == createRequest.AuctionId && 
                               rp.PositionName.ToLower() == createRequest.PositionName.ToLower());
            
            if (duplicateExists)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("A position with this name already exists in the auction");
                return conflictResponse;
            }

            // Create new roster position
            var newPosition = new RosterPosition
            {
                AuctionId = createRequest.AuctionId,
                PositionName = createRequest.PositionName.Trim(),
                SlotsPerTeam = createRequest.SlotsPerTeam,
                ColorCode = string.IsNullOrWhiteSpace(createRequest.ColorCode) ? "#0078d4" : createRequest.ColorCode,
                DisplayOrder = createRequest.DisplayOrder,
                IsFlexPosition = createRequest.IsFlexPosition
            };

            _context.RosterPositions.Add(newPosition);
            await _context.SaveChangesAsync();

            // Return created position
            var createdPosition = new
            {
                newPosition.RosterPositionId,
                newPosition.AuctionId,
                newPosition.PositionName,
                newPosition.SlotsPerTeam,
                newPosition.ColorCode,
                newPosition.DisplayOrder,
                newPosition.IsFlexPosition
            };

            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(createdPosition));

            _logger.LogInformation("Successfully created roster position {PositionName} for auction {AuctionId}", 
                createRequest.PositionName, createRequest.AuctionId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating roster position");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error creating roster position: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Deletes a roster position.
    /// </summary>
    /// <param name="req">The HTTP request containing position ID in route.</param>
    /// <param name="positionId">The roster position ID from route parameters.</param>
    /// <returns>Success or error response.</returns>
    [Function("DeleteRosterPosition")]
    public async Task<HttpResponseData> DeleteRosterPosition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "management/roster-positions/{positionId:int}")] HttpRequestData req,
        int positionId)
    {
        _logger.LogInformation("Deleting roster position {PositionId}", positionId);

        try
        {
            // Authenticate request
            if (!IsValidAdminRequest(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Find the position
            var position = await _context.RosterPositions
                .FirstOrDefaultAsync(rp => rp.RosterPositionId == positionId);

            if (position == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Roster position not found");
                return notFoundResponse;
            }

            // Check if position is in use (has draft picks)
            var positionInUse = await _context.DraftPicks
                .AnyAsync(dp => dp.RosterPositionId == positionId);

            if (positionInUse)
            {
                var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                await conflictResponse.WriteStringAsync("Cannot delete roster position that is in use by draft picks");
                return conflictResponse;
            }

            // Delete the position
            _context.RosterPositions.Remove(position);
            await _context.SaveChangesAsync();

            // Reorder remaining positions to fill gaps
            await ReorderPositionsAsync(position.AuctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Roster position deleted successfully");

            _logger.LogInformation("Successfully deleted roster position {PositionId}", positionId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting roster position {PositionId}", positionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error deleting roster position: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Reorders a roster position up or down in display order.
    /// </summary>
    /// <param name="req">The HTTP request containing reorder data in JSON body.</param>
    /// <param name="positionId">The roster position ID from route parameters.</param>
    /// <returns>Success or error response.</returns>
    [Function("ReorderRosterPosition")]
    public async Task<HttpResponseData> ReorderRosterPosition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/roster-positions/{positionId:int}/reorder")] HttpRequestData req,
        int positionId)
    {
        _logger.LogInformation("Reordering roster position {PositionId}", positionId);

        try
        {
            // Authenticate request
            if (!IsValidAdminRequest(req))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }

            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var reorderRequest = JsonSerializer.Deserialize<ReorderPositionRequest>(requestBody);

            if (reorderRequest == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            // Find the position to move
            var position = await _context.RosterPositions
                .FirstOrDefaultAsync(rp => rp.RosterPositionId == positionId);

            if (position == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Roster position not found");
                return notFoundResponse;
            }

            // Get all positions for this auction ordered by DisplayOrder
            var allPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == position.AuctionId)
                .OrderBy(rp => rp.DisplayOrder)
                .ToListAsync();

            var currentIndex = allPositions.FindIndex(p => p.RosterPositionId == positionId);
            var newIndex = currentIndex + reorderRequest.Direction;

            // Validate bounds
            if (newIndex < 0 || newIndex >= allPositions.Count)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Cannot move position beyond bounds");
                return badRequestResponse;
            }

            // Swap display orders
            var temp = allPositions[currentIndex].DisplayOrder;
            allPositions[currentIndex].DisplayOrder = allPositions[newIndex].DisplayOrder;
            allPositions[newIndex].DisplayOrder = temp;

            await _context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Position reordered successfully");

            _logger.LogInformation("Successfully reordered roster position {PositionId}", positionId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering roster position {PositionId}", positionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error reordering roster position: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Reorders all positions in an auction to eliminate gaps in DisplayOrder.
    /// </summary>
    /// <param name="auctionId">The auction ID to reorder positions for.</param>
    private async Task ReorderPositionsAsync(int auctionId)
    {
        var positions = await _context.RosterPositions
            .Where(rp => rp.AuctionId == auctionId)
            .OrderBy(rp => rp.DisplayOrder)
            .ToListAsync();

        for (int i = 0; i < positions.Count; i++)
        {
            positions[i].DisplayOrder = i + 1;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Validates the management authentication token in the request.
    /// </summary>
    /// <param name="req">The HTTP request to validate.</param>
    /// <returns>True if the request has a valid management token; false otherwise.</returns>
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
/// Request model for creating a new roster position.
/// </summary>
public class CreateRosterPositionRequest
{
    /// <summary>
    /// Gets or sets the auction ID this position belongs to.
    /// </summary>
    public int AuctionId { get; set; }

    /// <summary>
    /// Gets or sets the position name (e.g., "Power Conference", "SEC", "Flex").
    /// </summary>
    public string PositionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of roster slots of this type each team has.
    /// </summary>
    public int SlotsPerTeam { get; set; }

    /// <summary>
    /// Gets or sets the hex color code for UI display.
    /// </summary>
    public string ColorCode { get; set; } = "#0078d4";

    /// <summary>
    /// Gets or sets the display order for this position.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Gets or sets whether this is a flexible position accepting any school type.
    /// </summary>
    public bool IsFlexPosition { get; set; }
}

/// <summary>
/// Request model for reordering roster positions.
/// </summary>
public class ReorderPositionRequest
{
    /// <summary>
    /// Gets or sets the roster position ID to reorder.
    /// </summary>
    public int RosterPositionId { get; set; }

    /// <summary>
    /// Gets or sets the direction to move (-1 for up, +1 for down).
    /// </summary>
    public int Direction { get; set; }
}