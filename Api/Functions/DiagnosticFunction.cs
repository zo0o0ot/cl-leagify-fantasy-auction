using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Diagnostic endpoints for debugging data issues.
/// TEMPORARY - Remove after resolving team assignment and leave auction bugs.
/// </summary>
public class DiagnosticFunction(LeagifyAuctionDbContext context, ILogger<DiagnosticFunction> logger)
{
    /// <summary>
    /// Get basic auction info to help identify test data
    /// </summary>
    [Function("GetAuctionSummary")]
    public async Task<HttpResponseData> GetAuctionSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "diagnostic/auctions")] HttpRequestData req)
    {
        try
        {
            logger.LogInformation("Getting auction summary for debugging");

            var auctions = await context.Auctions
                .Select(a => new
                {
                    a.AuctionId,
                    a.Name,
                    a.JoinCode,
                    a.Status,
                    ParticipantCount = a.Users.Count(),
                    TeamCount = a.Teams.Count(),
                    a.CreatedDate
                })
                .OrderByDescending(a => a.CreatedDate)
                .Take(10) // Only show last 10 auctions
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                TotalAuctions = auctions.Count,
                Auctions = auctions
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting auction summary");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Create a test auction with participants and team assignments for debugging
    /// </summary>
    [Function("CreateTestData")]
    public async Task<HttpResponseData> CreateTestData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "diagnostic/create-test-data")] HttpRequestData req)
    {
        // Validate management token
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            logger.LogWarning("Unauthorized diagnostic request: {ErrorMessage}", validation.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            logger.LogInformation("Creating test auction with participants and team assignments");

            // Create test auction with unique identifiers to avoid conflicts
            var uniqueId = Guid.NewGuid().ToString("N")[..8]; // Use GUID for complete uniqueness
            var auction = new Api.Models.Auction
            {
                Name = $"DEBUG Test Auction {uniqueId}",
                JoinCode = $"DEBUG{uniqueId[..6].ToUpper()}",  // 6 chars from GUID
                MasterRecoveryCode = $"MASTER{uniqueId[^4..].ToUpper()}",  // Last 4 chars of GUID
                Status = "Draft",
                CreatedDate = DateTime.UtcNow
            };

            logger.LogInformation("Adding auction: {AuctionName} with JoinCode: {JoinCode}", auction.Name, auction.JoinCode);
            context.Auctions.Add(auction);
            await context.SaveChangesAsync();
            logger.LogInformation("Auction created with ID: {AuctionId}", auction.AuctionId);

            // Create test users first (required for team foreign keys)
            // Use separate GUIDs to ensure absolute uniqueness for display names
            var userGuid1 = Guid.NewGuid().ToString("N")[..8];
            var userGuid2 = Guid.NewGuid().ToString("N")[..8];

            var user1 = new Api.Models.User
            {
                AuctionId = auction.AuctionId,
                DisplayName = $"TestUser1_{userGuid1}",  // Absolutely unique user names
                SessionToken = Guid.NewGuid().ToString(),
                IsConnected = true,
                JoinedDate = DateTime.UtcNow,
                LastActiveDate = DateTime.UtcNow,
                IsReconnectionPending = false
            };

            var user2 = new Api.Models.User
            {
                AuctionId = auction.AuctionId,
                DisplayName = $"TestUser2_{userGuid2}",  // Absolutely unique user names
                SessionToken = Guid.NewGuid().ToString(),
                IsConnected = true,
                JoinedDate = DateTime.UtcNow,
                LastActiveDate = DateTime.UtcNow,
                IsReconnectionPending = false
            };

            logger.LogInformation("Adding users: {User1} and {User2}", user1.DisplayName, user2.DisplayName);
            context.Users.Add(user1);
            context.Users.Add(user2);
            await context.SaveChangesAsync();
            logger.LogInformation("Users created with IDs: {User1Id}, {User2Id}", user1.UserId, user2.UserId);

            // Create teams with valid user IDs
            var team1 = new Api.Models.Team
            {
                AuctionId = auction.AuctionId,
                UserId = user1.UserId, // Now we have a valid user ID
                TeamName = $"Alpha Team {userGuid1[..4]}",  // Make team names unique too
                Budget = 1000,
                RemainingBudget = 1000,
                NominationOrder = 1,
                IsActive = true
            };

            var team2 = new Api.Models.Team
            {
                AuctionId = auction.AuctionId,
                UserId = user2.UserId, // Now we have a valid user ID
                TeamName = $"Beta Team {userGuid2[..4]}",   // Make team names unique too
                Budget = 1000,
                RemainingBudget = 1000,
                NominationOrder = 2,
                IsActive = true
            };

            logger.LogInformation("Adding teams: {Team1} and {Team2}", team1.TeamName, team2.TeamName);
            context.Teams.Add(team1);
            context.Teams.Add(team2);
            await context.SaveChangesAsync();
            logger.LogInformation("Teams created with IDs: {Team1Id}, {Team2Id}", team1.TeamId, team2.TeamId);

            // Create user roles with team assignments
            var role1 = new Api.Models.UserRole
            {
                UserId = user1.UserId,
                Role = "TeamCoach",
                TeamId = team1.TeamId,
                AssignedDate = DateTime.UtcNow
            };

            var role2 = new Api.Models.UserRole
            {
                UserId = user2.UserId,
                Role = "TeamCoach",
                TeamId = team2.TeamId,
                AssignedDate = DateTime.UtcNow
            };

            logger.LogInformation("Adding user roles");
            context.UserRoles.Add(role1);
            context.UserRoles.Add(role2);
            await context.SaveChangesAsync();
            logger.LogInformation("User roles created successfully");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Test data created successfully",
                AuctionId = auction.AuctionId,
                JoinCode = auction.JoinCode,
                Users = new[]
                {
                    new { user1.UserId, user1.DisplayName, user1.SessionToken, TeamName = team1.TeamName },
                    new { user2.UserId, user2.DisplayName, user2.SessionToken, TeamName = team2.TeamName }
                }
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating test data");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error creating test data: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Get detailed participant data for a specific auction to debug team assignment issues
    /// </summary>
    [Function("GetDetailedParticipants")]
    public async Task<HttpResponseData> GetDetailedParticipants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "diagnostic/auction/{auctionId:int}/participants")] HttpRequestData req,
        int auctionId)
    {
        // Validate management token
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            logger.LogWarning("Unauthorized diagnostic request: {ErrorMessage}", validation.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            logger.LogInformation("Getting detailed participant data for auction {AuctionId}", auctionId);

            var participants = await context.Users
                .Where(u => u.AuctionId == auctionId)
                .Select(u => new
                {
                    u.UserId,
                    u.DisplayName,
                    u.IsConnected,
                    u.SessionToken,
                    u.JoinedDate,
                    Roles = u.UserRoles.Select(ur => new
                    {
                        ur.UserRoleId,
                        ur.Role,
                        ur.TeamId,
                        TeamName = ur.Team != null ? ur.Team.TeamName : null,
                        ur.AssignedDate,
                        TeamExists = ur.Team != null
                    }).ToList()
                })
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                AuctionId = auctionId,
                ParticipantCount = participants.Count,
                Participants = participants,
                DatabaseQueryInfo = new
                {
                    QueryExecuted = "Users.Include(UserRoles.Team)",
                    EntityFrameworkVersion = "EF Core 8.0"
                }
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting detailed participants for auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Reset an auction by removing all participants, teams, and roles - useful for clean testing
    /// </summary>
    [Function("ResetAuction")]
    public async Task<HttpResponseData> ResetAuction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "diagnostic/auction/{auctionId:int}/reset")] HttpRequestData req,
        int auctionId)
    {
        // Validate management token
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            logger.LogWarning("Unauthorized diagnostic request: {ErrorMessage}", validation.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            logger.LogInformation("Resetting auction {AuctionId} - removing all participants, teams, and roles", auctionId);

            // Verify auction exists
            var auction = await context.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            var deletionSummary = new
            {
                AuctionId = auctionId,
                AuctionName = auction.Name,
                DeletedItems = new Dictionary<string, int>()
            };

            // Delete user roles first (due to foreign keys)
            var userRoles = await context.UserRoles
                .Where(ur => ur.User.AuctionId == auctionId)
                .ToListAsync();

            context.UserRoles.RemoveRange(userRoles);
            deletionSummary.DeletedItems["UserRoles"] = userRoles.Count;

            // Delete users
            var users = await context.Users
                .Where(u => u.AuctionId == auctionId)
                .ToListAsync();

            context.Users.RemoveRange(users);
            deletionSummary.DeletedItems["Users"] = users.Count;

            // Delete teams
            var teams = await context.Teams
                .Where(t => t.AuctionId == auctionId)
                .ToListAsync();

            context.Teams.RemoveRange(teams);
            deletionSummary.DeletedItems["Teams"] = teams.Count;

            await context.SaveChangesAsync();

            logger.LogInformation("Successfully reset auction {AuctionId}: removed {UserCount} users, {TeamCount} teams, {RoleCount} roles",
                auctionId, users.Count, teams.Count, userRoles.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(deletionSummary);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error resetting auction: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Delete an entire auction and all related data - use carefully!
    /// </summary>
    [Function("DeleteAuction")]
    public async Task<HttpResponseData> DeleteAuction(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "diagnostic/auction/{auctionId:int}")] HttpRequestData req,
        int auctionId)
    {
        // Validate management token
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            logger.LogWarning("Unauthorized diagnostic request: {ErrorMessage}", validation.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            logger.LogInformation("DELETING entire auction {AuctionId} and all related data", auctionId);

            // Verify auction exists
            var auction = await context.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Auction not found");
                return notFoundResponse;
            }

            var deletionSummary = new
            {
                AuctionId = auctionId,
                AuctionName = auction.Name,
                DeletedItems = new Dictionary<string, int>()
            };

            // Delete in order due to foreign key constraints

            // 1. User roles
            var userRoles = await context.UserRoles
                .Where(ur => ur.User.AuctionId == auctionId)
                .ToListAsync();
            context.UserRoles.RemoveRange(userRoles);
            deletionSummary.DeletedItems["UserRoles"] = userRoles.Count;

            // 2. Users
            var users = await context.Users
                .Where(u => u.AuctionId == auctionId)
                .ToListAsync();
            context.Users.RemoveRange(users);
            deletionSummary.DeletedItems["Users"] = users.Count;

            // 3. Teams
            var teams = await context.Teams
                .Where(t => t.AuctionId == auctionId)
                .ToListAsync();
            context.Teams.RemoveRange(teams);
            deletionSummary.DeletedItems["Teams"] = teams.Count;

            // 4. Auction itself
            context.Auctions.Remove(auction);
            deletionSummary.DeletedItems["Auctions"] = 1;

            await context.SaveChangesAsync();

            logger.LogInformation("Successfully deleted auction {AuctionId} and all related data", auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(deletionSummary);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting auction {AuctionId}", auctionId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error deleting auction: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Bulk cleanup - delete all test auctions (those with "test", "debug", "sample" in name)
    /// </summary>
    [Function("CleanupTestAuctions")]
    public async Task<HttpResponseData> CleanupTestAuctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "diagnostic/cleanup-test-auctions")] HttpRequestData req)
    {
        // Validate management token
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            logger.LogWarning("Unauthorized diagnostic request: {ErrorMessage}", validation.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            logger.LogInformation("Starting bulk cleanup of test auctions");

            var testKeywords = new[] { "test", "debug", "sample", "demo" };
            var testAuctions = await context.Auctions
                .Where(a => testKeywords.Any(keyword => a.Name.ToLower().Contains(keyword)))
                .ToListAsync();

            var cleanupSummary = new
            {
                TotalTestAuctions = testAuctions.Count,
                DeletedAuctions = new List<object>(),
                TotalDeleted = new Dictionary<string, int>
                {
                    ["Auctions"] = 0,
                    ["Users"] = 0,
                    ["Teams"] = 0,
                    ["UserRoles"] = 0
                }
            };

            foreach (var auction in testAuctions)
            {
                logger.LogInformation("Deleting test auction: {AuctionName} (ID: {AuctionId})", auction.Name, auction.AuctionId);

                // Delete related data for this auction
                var userRoles = await context.UserRoles
                    .Where(ur => ur.User.AuctionId == auction.AuctionId)
                    .ToListAsync();
                context.UserRoles.RemoveRange(userRoles);

                var users = await context.Users
                    .Where(u => u.AuctionId == auction.AuctionId)
                    .ToListAsync();
                context.Users.RemoveRange(users);

                var teams = await context.Teams
                    .Where(t => t.AuctionId == auction.AuctionId)
                    .ToListAsync();
                context.Teams.RemoveRange(teams);

                context.Auctions.Remove(auction);

                cleanupSummary.TotalDeleted["Auctions"]++;
                cleanupSummary.TotalDeleted["Users"] += users.Count;
                cleanupSummary.TotalDeleted["Teams"] += teams.Count;
                cleanupSummary.TotalDeleted["UserRoles"] += userRoles.Count;

                ((List<object>)cleanupSummary.DeletedAuctions).Add(new
                {
                    auction.AuctionId,
                    auction.Name,
                    auction.JoinCode,
                    ParticipantsDeleted = users.Count,
                    TeamsDeleted = teams.Count,
                    RolesDeleted = userRoles.Count
                });
            }

            await context.SaveChangesAsync();

            logger.LogInformation("Bulk cleanup completed: deleted {AuctionCount} test auctions", testAuctions.Count);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(cleanupSummary);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bulk test auction cleanup");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error during cleanup: {ex.Message}");
            return errorResponse;
        }
    }

    /// <summary>
    /// Fix duplicate TeamId issue in auction 37
    /// SPECIFIC FIX: Team 1 has correct NominationOrder=1, Team 6 should get new TeamId
    /// </summary>
    [Function("FixDuplicateTeamIds")]
    public async Task<HttpResponseData> FixDuplicateTeamIds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "diagnostic/auction/{auctionId:int}/fix-duplicate-teams")] HttpRequestData req,
        int auctionId)
    {
        // Validate management token
        var validation = ManagementAuthFunction.ValidateManagementToken(req);
        if (!validation.IsValid)
        {
            logger.LogWarning("Unauthorized diagnostic request: {ErrorMessage}", validation.ErrorMessage);
            var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorizedResponse.WriteStringAsync("Unauthorized");
            return unauthorizedResponse;
        }

        try
        {
            logger.LogInformation("Fixing duplicate TeamId issue for auction {AuctionId}", auctionId);

            // Get all teams for this auction
            var teams = await context.Teams
                .Where(t => t.AuctionId == auctionId)
                .OrderBy(t => t.NominationOrder)
                .ToListAsync();

            logger.LogInformation("Found {TeamCount} teams: {Teams}",
                teams.Count,
                string.Join(", ", teams.Select(t => $"{t.TeamName}(ID:{t.TeamId},Order:{t.NominationOrder})")));

            // Find duplicate TeamIds
            var duplicateGroups = teams.GroupBy(t => t.TeamId)
                .Where(g => g.Count() > 1)
                .ToList();

            if (!duplicateGroups.Any())
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("No duplicate TeamIds found");
                return response;
            }

            var fixedCount = 0;
            foreach (var group in duplicateGroups)
            {
                var duplicateTeams = group.OrderBy(t => t.NominationOrder).ToList();
                logger.LogWarning("Found duplicate TeamId {TeamId} used by {Count} teams: {TeamNames}",
                    group.Key, duplicateTeams.Count, string.Join(", ", duplicateTeams.Select(t => t.TeamName)));

                // Keep the first team (lowest NominationOrder) with the original TeamId
                // Assign new TeamIds to the others
                for (int i = 1; i < duplicateTeams.Count; i++)
                {
                    var teamToFix = duplicateTeams[i];
                    var newTeamId = await GetNextAvailableTeamId(auctionId);

                    logger.LogInformation("Fixing {TeamName}: changing TeamId from {OldId} to {NewId}",
                        teamToFix.TeamName, teamToFix.TeamId, newTeamId);

                    teamToFix.TeamId = newTeamId;
                    fixedCount++;
                }
            }

            await context.SaveChangesAsync();

            logger.LogInformation("Successfully fixed {FixedCount} duplicate TeamIds", fixedCount);

            var finalResponse = req.CreateResponse(HttpStatusCode.OK);
            await finalResponse.WriteAsJsonAsync(new
            {
                Success = true,
                Message = $"Fixed {fixedCount} duplicate TeamIds",
                FixedTeams = duplicateGroups.SelectMany(g => g.Skip(1)).Select(t => new
                {
                    TeamName = t.TeamName,
                    OldTeamId = t.TeamId,
                    NewTeamId = t.TeamId,
                    NominationOrder = t.NominationOrder
                })
            });

            return finalResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fixing duplicate TeamIds");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    private async Task<int> GetNextAvailableTeamId(int auctionId)
    {
        var existingIds = await context.Teams
            .Where(t => t.AuctionId == auctionId)
            .Select(t => t.TeamId)
            .ToListAsync();

        // Find the first available ID starting from 1
        for (int i = 1; i <= 100; i++)
        {
            if (!existingIds.Contains(i))
            {
                return i;
            }
        }

        // If somehow we can't find an available ID, use a high number
        return existingIds.Max() + 1;
    }
}