using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Azure Functions for handling auction join operations and user session management.
/// Provides endpoints for users to join auctions with join codes and manage their session state.
/// </summary>
public class AuctionJoinFunction(ILoggerFactory loggerFactory, LeagifyAuctionDbContext context)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AuctionJoinFunction>();

    /// <summary>
    /// Allows a user to join an auction using a join code and display name.
    /// Creates a new user record or updates existing one, handles duplicate name validation.
    /// </summary>
    /// <param name="req">HTTP request containing join code and display name</param>
    /// <returns>User session information including session token</returns>
    [Function("AuctionJoin")]
    public async Task<HttpResponseData> JoinAuction([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/join")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var joinRequest = JsonSerializer.Deserialize<JoinAuctionRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (joinRequest == null)
            {
                _logger.LogWarning("Join auction request body is null or invalid");
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request data");
            }

            // Validate input
            var validationContext = new ValidationContext(joinRequest);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(joinRequest, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(x => x.ErrorMessage));
                _logger.LogWarning("Join auction validation failed: {Errors}", errors);
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, $"Validation failed: {errors}");
            }

            // Clean up input data
            var joinCode = joinRequest.JoinCode.Trim().ToUpperInvariant();
            var displayName = joinRequest.DisplayName.Trim();

            _logger.LogInformation("Processing join request for code: {JoinCode}, name: {DisplayName}", joinCode, displayName);

            // Find auction by join code
            var auction = await context.Auctions
                .FirstOrDefaultAsync(a => a.JoinCode == joinCode);

            if (auction == null)
            {
                _logger.LogWarning("Auction not found for join code: {JoinCode}", joinCode);
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Join code not found");
            }

            // Check if auction is joinable
            if (auction.Status != "Draft" && auction.Status != "InProgress")
            {
                _logger.LogWarning("Auction {AuctionId} is not joinable, status: {Status}", auction.AuctionId, auction.Status);
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "This auction is no longer accepting participants");
            }

            // Check for existing user with same display name (case-insensitive)
            var existingUser = await context.Users
                .Where(u => u.AuctionId == auction.AuctionId)
                .FirstOrDefaultAsync(u => u.DisplayName.ToLower() == displayName.ToLower());

            // Generate session token
            var sessionToken = GenerateSessionToken();

            User user;
            if (existingUser != null)
            {
                // Update existing user's connection status and last active time
                existingUser.IsConnected = true;
                existingUser.LastActiveDate = DateTime.UtcNow;
                existingUser.IsReconnectionPending = false;
                existingUser.SessionToken = sessionToken;
                user = existingUser;
                
                _logger.LogInformation("Existing user {UserId} ({DisplayName}) reconnected to auction {AuctionId}", 
                    user.UserId, displayName, auction.AuctionId);
            }
            else
            {
                // Create new user record
                user = new User
                {
                    AuctionId = auction.AuctionId,
                    DisplayName = displayName,
                    IsConnected = true,
                    JoinedDate = DateTime.UtcNow,
                    LastActiveDate = DateTime.UtcNow,
                    IsReconnectionPending = false,
                    SessionToken = sessionToken
                };

                context.Users.Add(user);
                _logger.LogInformation("New user created with display name {DisplayName} in auction {AuctionId}", 
                    displayName, auction.AuctionId);
            }

            await context.SaveChangesAsync();

            var actionType = existingUser != null ? "reconnected to" : "joined";
            _logger.LogInformation("User {UserId} successfully {ActionType} auction {AuctionId} as {DisplayName}", 
                user.UserId, actionType, auction.AuctionId, displayName);

            // Create response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new JoinAuctionResponse
            {
                UserId = user.UserId,
                AuctionId = auction.AuctionId,
                DisplayName = user.DisplayName,
                SessionToken = sessionToken,
                AuctionName = auction.Name
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing join auction request");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Validates an existing user session to check if it's still active and valid.
    /// Used for automatic reconnection and session persistence.
    /// </summary>
    /// <param name="req">HTTP request with session token in X-Auction-Token header</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <returns>Session validation result</returns>
    [Function("ValidateSession")]
    public async Task<HttpResponseData> ValidateSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId:int}/validate-session")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            var sessionToken = req.Headers.GetValues("X-Auction-Token").FirstOrDefault();
            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogWarning("Session validation attempted without token for auction {AuctionId}", auctionId);
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Session token required");
            }

            // Validate session token against stored user session
            var user = await context.Users
                .Where(u => u.AuctionId == auctionId && u.SessionToken == sessionToken)
                .FirstOrDefaultAsync();

            if (user != null)
            {
                // Update last active date
                user.LastActiveDate = DateTime.UtcNow;
                await context.SaveChangesAsync();

                _logger.LogInformation("Session validated for user in auction {AuctionId}", auctionId);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { IsValid = true, UserId = user.UserId });
                return response;
            }

            _logger.LogWarning("Invalid session for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Invalid session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Session validation failed");
        }
    }

    /// <summary>
    /// Gets all participants in an auction with their current roles and status.
    /// Used by both admin interfaces and participant views.
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <returns>List of auction participants with role information</returns>
    [Function("GetAuctionParticipants")]
    public async Task<HttpResponseData> GetAuctionParticipants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId:int}/participants")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            _logger.LogInformation("Getting participants for auction {AuctionId}", auctionId);

            var participants = await context.Users
                .Where(u => u.AuctionId == auctionId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Team)
                .Select(u => new ParticipantDto
                {
                    UserId = u.UserId,
                    DisplayName = u.DisplayName,
                    IsConnected = u.IsConnected,
                    JoinedDate = u.JoinedDate,
                    LastActiveDate = u.LastActiveDate,
                    IsReconnectionPending = u.IsReconnectionPending,
                    Roles = u.UserRoles.Select(ur => new RoleDto
                    {
                        UserRoleId = ur.UserRoleId,
                        Role = ur.Role,
                        TeamId = ur.TeamId,
                        TeamName = ur.Team != null ? ur.Team.TeamName : null,
                        AssignedDate = ur.AssignedDate
                    }).ToList()
                })
                .OrderBy(p => p.JoinedDate)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(participants);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting participants for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to get participants");
        }
    }

    /// <summary>
    /// Generates a session token for user authentication.
    /// In a production system, this would be more sophisticated with proper JWT or similar.
    /// </summary>
    private static string GenerateSessionToken()
    {
        return Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    }

    /// <summary>
    /// Creates a standardized error response with logging.
    /// </summary>
    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteStringAsync(message);
        return response;
    }
}

/// <summary>
/// Request model for joining an auction.
/// </summary>
public class JoinAuctionRequest
{
    [Required(ErrorMessage = "Join code is required")]
    [MinLength(3, ErrorMessage = "Join code must be at least 3 characters")]
    [MaxLength(10, ErrorMessage = "Join code cannot exceed 10 characters")]
    public string JoinCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required")]
    [MinLength(2, ErrorMessage = "Display name must be at least 2 characters")]
    [MaxLength(50, ErrorMessage = "Display name cannot exceed 50 characters")]
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Response model for successful auction join.
/// </summary>
public class JoinAuctionResponse
{
    public int UserId { get; set; }
    public int AuctionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string AuctionName { get; set; } = string.Empty;
}

/// <summary>
/// DTO for auction participant information.
/// </summary>
public class ParticipantDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime JoinedDate { get; set; }
    public DateTime LastActiveDate { get; set; }
    public bool IsReconnectionPending { get; set; }
    public List<RoleDto> Roles { get; set; } = new();
}

/// <summary>
/// DTO for user role information.
/// </summary>
public class RoleDto
{
    public int UserRoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime AssignedDate { get; set; }
}