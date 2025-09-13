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
        try
        {
            logger.LogInformation("Creating test auction with participants and team assignments");

            // Create test auction
            var auction = new Api.Models.Auction
            {
                Name = "DEBUG Test Auction",
                JoinCode = "DEBUG1",
                MasterRecoveryCode = "MASTER1",
                Status = "Draft",
                CreatedDate = DateTime.UtcNow
            };

            context.Auctions.Add(auction);
            await context.SaveChangesAsync();

            // Create teams
            var team1 = new Api.Models.Team
            {
                AuctionId = auction.AuctionId,
                UserId = 0, // Temporary - will be assigned later
                TeamName = "Alpha Team",
                Budget = 1000,
                RemainingBudget = 1000,
                NominationOrder = 1,
                IsActive = true
            };

            var team2 = new Api.Models.Team
            {
                AuctionId = auction.AuctionId,
                UserId = 0,
                TeamName = "Beta Team",
                Budget = 1000,
                RemainingBudget = 1000,
                NominationOrder = 2,
                IsActive = true
            };

            context.Teams.Add(team1);
            context.Teams.Add(team2);
            await context.SaveChangesAsync();

            // Create test users
            var user1 = new Api.Models.User
            {
                AuctionId = auction.AuctionId,
                DisplayName = "TestUser1",
                SessionToken = Guid.NewGuid().ToString(),
                IsConnected = true,
                JoinedDate = DateTime.UtcNow,
                LastActiveDate = DateTime.UtcNow,
                IsReconnectionPending = false
            };

            var user2 = new Api.Models.User
            {
                AuctionId = auction.AuctionId,
                DisplayName = "TestUser2",
                SessionToken = Guid.NewGuid().ToString(),
                IsConnected = true,
                JoinedDate = DateTime.UtcNow,
                LastActiveDate = DateTime.UtcNow,
                IsReconnectionPending = false
            };

            context.Users.Add(user1);
            context.Users.Add(user2);
            await context.SaveChangesAsync();

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

            context.UserRoles.Add(role1);
            context.UserRoles.Add(role2);
            await context.SaveChangesAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "Test data created successfully",
                AuctionId = auction.AuctionId,
                JoinCode = auction.JoinCode,
                Users = new[]
                {
                    new { user1.UserId, user1.DisplayName, user1.SessionToken, TeamName = "Alpha Team" },
                    new { user2.UserId, user2.DisplayName, user2.SessionToken, TeamName = "Beta Team" }
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
}