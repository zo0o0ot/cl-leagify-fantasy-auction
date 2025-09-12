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
/// Azure Functions for managing user roles and team assignments within auctions.
/// Provides endpoints for auction masters to assign roles, manage teams, and handle permissions.
/// </summary>
public class RoleManagementFunction(ILoggerFactory loggerFactory, LeagifyAuctionDbContext context)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<RoleManagementFunction>();

    /// <summary>
    /// Assigns a role to a user in an auction. Only accessible by management authentication.
    /// Supports assigning AuctionMaster, TeamCoach, ProxyCoach, and Viewer roles.
    /// </summary>
    /// <param name="req">HTTP request containing role assignment data</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <param name="userId">User ID from route</param>
    /// <returns>Role assignment result</returns>
    [Function("AssignUserRole")]
    public async Task<HttpResponseData> AssignUserRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auctions/{auctionId:int}/users/{userId:int}/roles")] HttpRequestData req,
        int auctionId, int userId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var roleRequest = JsonSerializer.Deserialize<AssignRoleRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (roleRequest == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request data");
            }

            _logger.LogInformation("Assigning role {Role} to user {UserId} in auction {AuctionId}", 
                roleRequest.Role, userId, auctionId);

            // Validate user exists in auction
            var user = await context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.UserId == userId && u.AuctionId == auctionId);

            if (user == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "User not found in this auction");
            }

            // Validate role type
            var validRoles = new[] { "AuctionMaster", "TeamCoach", "ProxyCoach", "Viewer" };
            if (!validRoles.Contains(roleRequest.Role))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid role type");
            }

            // For team-based roles, validate team assignment
            Team? team = null;
            if ((roleRequest.Role == "TeamCoach" || roleRequest.Role == "ProxyCoach") && roleRequest.TeamId.HasValue)
            {
                team = await context.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == roleRequest.TeamId.Value && t.AuctionId == auctionId);

                if (team == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid team ID");
                }
            }

            // Check if user already has this exact role
            var existingRole = user.UserRoles
                .FirstOrDefault(ur => ur.Role == roleRequest.Role && ur.TeamId == roleRequest.TeamId);

            if (existingRole != null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.Conflict, "User already has this role assignment");
            }

            // Create new role assignment
            var userRole = new UserRole
            {
                UserId = userId,
                TeamId = roleRequest.TeamId,
                Role = roleRequest.Role,
                AssignedDate = DateTime.UtcNow
            };

            context.UserRoles.Add(userRole);
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully assigned role {Role} to user {UserId} in auction {AuctionId}", 
                roleRequest.Role, userId, auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new AssignRoleResponse
            {
                UserRoleId = userRole.UserRoleId,
                UserId = userId,
                Role = roleRequest.Role,
                TeamId = roleRequest.TeamId,
                TeamName = team?.TeamName,
                AssignedDate = userRole.AssignedDate
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role to user {UserId} in auction {AuctionId}", userId, auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to assign role");
        }
    }

    /// <summary>
    /// Removes a role assignment from a user. Only accessible by management authentication.
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <param name="userId">User ID from route</param>
    /// <param name="roleId">UserRole ID from route</param>
    /// <returns>Role removal result</returns>
    [Function("RemoveUserRole")]
    public async Task<HttpResponseData> RemoveUserRole(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "management/auctions/{auctionId:int}/users/{userId:int}/roles/{roleId:int}")] HttpRequestData req,
        int auctionId, int userId, int roleId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            _logger.LogInformation("Removing role {RoleId} from user {UserId} in auction {AuctionId}", 
                roleId, userId, auctionId);

            var userRole = await context.UserRoles
                .Include(ur => ur.User)
                .FirstOrDefaultAsync(ur => ur.UserRoleId == roleId && 
                                          ur.UserId == userId && 
                                          ur.User.AuctionId == auctionId);

            if (userRole == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Role assignment not found");
            }

            context.UserRoles.Remove(userRole);
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully removed role {RoleId} from user {UserId}", roleId, userId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Role removed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId} in auction {AuctionId}", 
                roleId, userId, auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to remove role");
        }
    }

    /// <summary>
    /// Deletes a user from an auction, including all their roles and assignments.
    /// Only accessible by management authentication.
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <param name="userId">User ID from route</param>
    /// <returns>User deletion result</returns>
    [Function("DeleteAuctionUser")]
    public async Task<HttpResponseData> DeleteAuctionUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "management/auctions/{auctionId:int}/users/{userId:int}")] HttpRequestData req,
        int auctionId, int userId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            _logger.LogInformation("Deleting user {UserId} from auction {AuctionId}", userId, auctionId);

            var user = await context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.UserId == userId && u.AuctionId == auctionId);

            if (user == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "User not found in this auction");
            }

            // Remove all roles first
            context.UserRoles.RemoveRange(user.UserRoles);
            
            // Remove the user
            context.Users.Remove(user);
            
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted user {UserId} ({DisplayName}) from auction {AuctionId}", 
                userId, user.DisplayName, auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User deleted successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId} from auction {AuctionId}", userId, auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to delete user");
        }
    }

    /// <summary>
    /// Creates or updates team assignments for an auction.
    /// Manages the teams that participate in bidding and assigns coaches to them.
    /// </summary>
    /// <param name="req">HTTP request containing team configuration</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <returns>Team creation/update result</returns>
    [Function("ManageAuctionTeams")]
    public async Task<HttpResponseData> ManageAuctionTeams(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auctions/{auctionId:int}/teams")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var teamRequest = JsonSerializer.Deserialize<ManageTeamsRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (teamRequest?.Teams == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid team data");
            }

            _logger.LogInformation("Managing teams for auction {AuctionId}, {TeamCount} teams", 
                auctionId, teamRequest.Teams.Count);

            // Validate auction exists
            var auction = await context.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Auction not found");
            }

            // Get existing teams
            var existingTeams = await context.Teams
                .Where(t => t.AuctionId == auctionId)
                .ToListAsync();

            var updatedTeams = new List<Team>();

            // Process each team in the request
            for (int i = 0; i < teamRequest.Teams.Count; i++)
            {
                var teamData = teamRequest.Teams[i];
                
                Team team;
                if (teamData.TeamId > 0)
                {
                    // Update existing team
                    team = existingTeams.FirstOrDefault(t => t.TeamId == teamData.TeamId);
                    if (team == null)
                    {
                        return await CreateErrorResponse(req, HttpStatusCode.BadRequest, $"Team ID {teamData.TeamId} not found");
                    }
                    team.TeamName = teamData.TeamName;
                    team.Budget = teamData.Budget;
                    team.RemainingBudget = teamData.Budget; // Reset remaining budget when budget changes
                    team.NominationOrder = i + 1;
                }
                else
                {
                    // Create new team - need a user to assign it to initially
                    // For now, we'll create placeholder teams and assign users later
                    team = new Team
                    {
                        AuctionId = auctionId,
                        UserId = 1, // Placeholder - will be updated when roles are assigned
                        TeamName = teamData.TeamName,
                        Budget = teamData.Budget,
                        RemainingBudget = teamData.Budget,
                        NominationOrder = i + 1,
                        IsActive = true
                    };
                    context.Teams.Add(team);
                }
                
                updatedTeams.Add(team);
            }

            // Remove teams that are no longer in the request
            var teamsToRemove = existingTeams
                .Where(et => !teamRequest.Teams.Any(rt => rt.TeamId == et.TeamId))
                .ToList();

            foreach (var teamToRemove in teamsToRemove)
            {
                context.Teams.Remove(teamToRemove);
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully managed {TeamCount} teams for auction {AuctionId}", 
                updatedTeams.Count, auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new ManageTeamsResponse
            {
                Teams = updatedTeams.Select(t => new TeamDto
                {
                    TeamId = t.TeamId,
                    TeamName = t.TeamName,
                    Budget = t.Budget,
                    NominationOrder = t.NominationOrder,
                    IsActive = t.IsActive
                }).ToList()
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing teams for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to manage teams");
        }
    }

    /// <summary>
    /// Validates management authentication by checking for the X-Management-Token header.
    /// </summary>
    private async Task<bool> ValidateManagementAuth(HttpRequestData req)
    {
        try
        {
            var token = req.Headers.GetValues("X-Management-Token").FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            // For now, we'll do basic validation - in production, you'd validate the actual token
            // This matches the pattern used in other management functions
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
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
/// Request model for assigning a role to a user.
/// </summary>
public class AssignRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
    
    public int? TeamId { get; set; }
}

/// <summary>
/// Response model for role assignment.
/// </summary>
public class AssignRoleResponse
{
    public int UserRoleId { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime AssignedDate { get; set; }
}

/// <summary>
/// Request model for managing auction teams.
/// </summary>
public class ManageTeamsRequest
{
    public List<TeamRequestDto> Teams { get; set; } = new();
}

/// <summary>
/// Response model for team management.
/// </summary>
public class ManageTeamsResponse
{
    public List<TeamDto> Teams { get; set; } = new();
}

/// <summary>
/// DTO for team data in requests.
/// </summary>
public class TeamRequestDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal Budget { get; set; }
}

/// <summary>
/// DTO for team data in responses.
/// </summary>
public class TeamDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public int NominationOrder { get; set; }
    public bool IsActive { get; set; }
}