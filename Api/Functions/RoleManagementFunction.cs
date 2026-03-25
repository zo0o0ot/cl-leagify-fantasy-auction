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
            if (roleRequest.Role == "TeamCoach" || roleRequest.Role == "ProxyCoach")
            {
                if (!roleRequest.TeamId.HasValue)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Team assignment is required for Coach roles");
                }

                // Look up by NominationOrder since that represents the logical team position (1-6)
                // TeamId from UI represents the desired team number, not the actual database ID
                team = await context.Teams
                    .FirstOrDefaultAsync(t => t.NominationOrder == roleRequest.TeamId.Value && t.AuctionId == auctionId);

                if (team == null)
                {
                    // Create team for the specified position if it doesn't exist
                    _logger.LogInformation("Team position {TeamPosition} not found, creating team", roleRequest.TeamId.Value);

                    team = new Team
                    {
                        AuctionId = auctionId,
                        UserId = userId, // Assign to the user being given TeamCoach role
                        TeamName = $"Team {roleRequest.TeamId.Value}",
                        Budget = 200m,
                        RemainingBudget = 200m,
                        NominationOrder = roleRequest.TeamId.Value,
                        IsActive = true
                    };

                    context.Teams.Add(team);
                    await context.SaveChangesAsync();

                    // Update roleRequest with the actual created TeamId
                    roleRequest.TeamId = team.TeamId;

                    _logger.LogInformation("Created team {TeamName} with ID {TeamId} for auction {AuctionId}",
                        team.TeamName, team.TeamId, auctionId);
                }

                // Auto-rename default team name and claim ownership if unowned
                if (team.TeamName == $"Team {team.NominationOrder}")
                {
                    var newName = $"Team {user.DisplayName}";
                    _logger.LogInformation("Auto-renaming default team name from {OldName} to {NewName}", team.TeamName, newName);
                    team.TeamName = newName;
                }
                
                if (team.UserId == null)
                {
                    _logger.LogInformation("Assigning ownership of Team {TeamId} to User {UserId}", team.TeamId, userId);
                    team.UserId = userId;
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
                ProxyAlias = roleRequest.Role == "ProxyCoach" ? roleRequest.ProxyAlias : null,
                AssignedDate = DateTime.UtcNow
            };

            context.UserRoles.Add(userRole);
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully assigned role {Role} to user {UserId} in auction {AuctionId}{ProxyAlias}",
                roleRequest.Role, userId, auctionId,
                string.IsNullOrEmpty(userRole.ProxyAlias) ? "" : $" (ProxyAlias: {userRole.ProxyAlias})");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new AssignRoleResponse
            {
                UserRoleId = userRole.UserRoleId,
                UserId = userId,
                Role = roleRequest.Role,
                TeamId = roleRequest.TeamId,
                TeamName = team?.TeamName,
                ProxyAlias = userRole.ProxyAlias,
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

            _logger.LogInformation("Starting delete process for user {UserId} from auction {AuctionId}", userId, auctionId);

            // Check if auction exists first
            var auctionExists = await context.Auctions.AnyAsync(a => a.AuctionId == auctionId);
            if (!auctionExists)
            {
                _logger.LogWarning("Auction {AuctionId} not found", auctionId);
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Auction not found");
            }

            _logger.LogDebug("Auction {AuctionId} exists, searching for user {UserId}", auctionId, userId);

            // First, get user without navigation properties to avoid tracking issues
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.AuctionId == auctionId);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found in auction {AuctionId}", userId, auctionId);
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "User not found in this auction");
            }

            _logger.LogInformation("Found user {UserId} ({DisplayName})", userId, user.DisplayName);

            // Check ALL foreign key references before deletion (comprehensive constraint checking)
            var bidHistories = await context.BidHistories.Where(bh => bh.UserId == userId).CountAsync();
            var nominatedPicks = await context.DraftPicks.Where(dp => dp.NominatedByUserId == userId).CountAsync();
            var wonPicks = await context.DraftPicks.Where(dp => dp.WonByUserId == userId).CountAsync();
            var adminActions = await context.AdminActions.Where(aa => aa.AdminUserId == userId).CountAsync();
            var nominationOrders = await context.NominationOrders.Where(no => no.UserId == userId).CountAsync();

            // MISSING CONSTRAINTS - These were causing the 500 errors!
            var ownedTeams = await context.Teams.Where(t => t.UserId == userId).CountAsync();
            var createdAuctions = await context.Auctions.Where(a => a.CreatedByUserId == userId).CountAsync();
            var currentNominatorAuctions = await context.Auctions.Where(a => a.CurrentNominatorUserId == userId).CountAsync();
            var currentHighBidderAuctions = await context.Auctions.Where(a => a.CurrentHighBidderUserId == userId).CountAsync();

            _logger.LogInformation("User {UserId} foreign key references - BidHistories: {BidCount}, NominatedPicks: {NominatedCount}, WonPicks: {WonCount}, AdminActions: {AdminCount}, NominationOrders: {NominationCount}, OwnedTeams: {OwnedTeams}, CreatedAuctions: {CreatedAuctions}, CurrentNominator: {CurrentNominator}, CurrentHighBidder: {CurrentHighBidder}",
                userId, bidHistories, nominatedPicks, wonPicks, adminActions, nominationOrders, ownedTeams, createdAuctions, currentNominatorAuctions, currentHighBidderAuctions);

            // Check if user has ANY foreign key references that prevent deletion
            var hasConstraints = bidHistories > 0 || nominatedPicks > 0 || wonPicks > 0 || adminActions > 0 || nominationOrders > 0 || ownedTeams > 0 || createdAuctions > 0 || currentNominatorAuctions > 0 || currentHighBidderAuctions > 0;

            if (hasConstraints)
            {
                var constraints = new List<string>();
                if (bidHistories > 0) constraints.Add($"{bidHistories} bid histories");
                if (nominatedPicks > 0) constraints.Add($"{nominatedPicks} nominations");
                if (wonPicks > 0) constraints.Add($"{wonPicks} won picks");
                if (adminActions > 0) constraints.Add($"{adminActions} admin actions");
                if (nominationOrders > 0) constraints.Add($"{nominationOrders} nomination orders");
                if (ownedTeams > 0) constraints.Add($"{ownedTeams} owned teams");
                if (createdAuctions > 0) constraints.Add($"{createdAuctions} created auctions");
                if (currentNominatorAuctions > 0) constraints.Add($"current nominator in {currentNominatorAuctions} auctions");
                if (currentHighBidderAuctions > 0) constraints.Add($"current high bidder in {currentHighBidderAuctions} auctions");

                var constraintMessage = string.Join(", ", constraints);
                _logger.LogWarning("❌ Cannot delete user {UserId} due to foreign key constraints: {Constraints}", userId, constraintMessage);
                return await CreateErrorResponse(req, HttpStatusCode.Conflict, $"Cannot delete user who has auction dependencies: {constraintMessage}");
            }

            // Remove all roles first - query separately to avoid EF tracking conflicts
            var userRoles = await context.UserRoles
                .Where(ur => ur.UserId == userId)
                .ToListAsync();

            if (userRoles.Any())
            {
                _logger.LogInformation("Removing {RoleCount} roles for user {UserId}", userRoles.Count, userId);
                context.UserRoles.RemoveRange(userRoles);
            }
            else
            {
                _logger.LogInformation("User {UserId} has no roles to remove", userId);
            }

            // Remove the user
            _logger.LogInformation("Removing user {UserId} from context", userId);
            context.Users.Remove(user);

            _logger.LogInformation("Saving changes to database...");
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
    /// Gets all teams for an auction.
    /// Used by role assignment interface to populate team dropdown options.
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <returns>List of teams in the auction</returns>
    [Function("GetAuctionTeams")]
    public async Task<HttpResponseData> GetAuctionTeams(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auctions/{auctionId:int}/teams")] HttpRequestData req,
        int auctionId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            _logger.LogInformation("Getting teams for auction {AuctionId}", auctionId);

            // Check if auction exists
            var auction = await context.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "Auction not found");
            }

            // Get existing teams from database
            var existingTeams = await context.Teams
                .Where(t => t.AuctionId == auctionId && t.IsActive)
                .OrderBy(t => t.NominationOrder)
                .Select(t => new TeamDto
                {
                    TeamId = t.TeamId,
                    TeamName = t.TeamName ?? $"Team {t.NominationOrder}",
                    Budget = t.Budget,
                    NominationOrder = t.NominationOrder,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            _logger.LogInformation("Found {ExistingTeamCount} existing teams: {TeamDetails}",
                existingTeams.Count,
                string.Join(", ", existingTeams.Select(t => $"{t.TeamName}(ID:{t.TeamId},Order:{t.NominationOrder})")));

            // Check for duplicate NominationOrders in the database
            var duplicateOrders = existingTeams.GroupBy(t => t.NominationOrder)
                .Where(g => g.Count() > 1)
                .Select(g => new { Order = g.Key, Count = g.Count(), Teams = g.ToList() });

            if (duplicateOrders.Any())
            {
                _logger.LogWarning("🔥 FOUND DUPLICATE NOMINATION ORDERS IN DATABASE:");
                foreach (var dupe in duplicateOrders)
                {
                    _logger.LogWarning("  Order {Order} has {Count} teams: {Teams}",
                        dupe.Order, dupe.Count, string.Join(", ", dupe.Teams.Select(t => $"{t.TeamName}(ID:{t.TeamId})")));
                }
            }

            // Return ONLY the actual teams configured for this auction
            // We no longer want to pad with 6 placeholder teams since the user defines the team count during Setup
            var teams = existingTeams.ToList();
            
            _logger.LogInformation("Final team list: {TeamList}",
                string.Join(", ", teams.Select(t => $"{t.TeamName}(Order:{t.NominationOrder},ID:{t.TeamId})")));

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new ManageTeamsResponse
            {
                Teams = teams
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting teams for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to get teams");
        }
    }

    /// <summary>
    /// Marks a user as offline in an auction.
    /// Used for testing connection status and enabling delete functionality.
    /// </summary>
    /// <param name="req">HTTP request</param>
    /// <param name="auctionId">Auction ID from route</param>
    /// <param name="userId">User ID from route</param>
    /// <returns>User offline status result</returns>
    [Function("MarkUserOffline")]
    public async Task<HttpResponseData> MarkUserOffline(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auctions/{auctionId:int}/users/{userId:int}/offline")] HttpRequestData req,
        int auctionId, int userId)
    {
        try
        {
            // Validate management authentication
            if (!await ValidateManagementAuth(req))
            {
                return await CreateErrorResponse(req, HttpStatusCode.Unauthorized, "Management authentication required");
            }

            _logger.LogInformation("Marking user {UserId} as offline in auction {AuctionId}", userId, auctionId);

            var user = await context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.AuctionId == auctionId);

            if (user == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, "User not found in this auction");
            }

            // Mark user as offline
            user.IsConnected = false;
            user.LastActiveDate = DateTime.UtcNow;
            
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully marked user {UserId} ({DisplayName}) as offline in auction {AuctionId}", 
                userId, user.DisplayName, auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("User marked as offline");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking user {UserId} offline in auction {AuctionId}", userId, auctionId);
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to mark user offline");
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
                        UserId = null, // Placeholder - will be updated when roles are assigned
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
            _logger.LogError(ex, "Error managing teams for auction {AuctionId}. Exception: {Message}, Inner: {InnerMessage}",
                auctionId, ex.Message, ex.InnerException?.Message ?? "none");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError,
                $"Failed to manage teams: {ex.Message}" + (ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : ""));
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

    /// <summary>
    /// Optional custom alias for Proxy Coach roles (e.g., "Cyber-Ross")
    /// </summary>
    public string? ProxyAlias { get; set; }
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
    public string? ProxyAlias { get; set; }
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