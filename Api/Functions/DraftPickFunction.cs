using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using System.Net;
using System.Text.Json;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Handles draft pick management including roster position assignment and results compilation.
/// Manages the final stage of auction where schools are assigned to specific roster positions.
/// </summary>
public class DraftPickFunction(LeagifyAuctionDbContext context, ILogger<DraftPickFunction> logger)
{
    private readonly LeagifyAuctionDbContext _context = context;
    private readonly ILogger<DraftPickFunction> _logger = logger;

    /// <summary>
    /// Gets all draft picks for an auction with their current assignments.
    /// Returns complete draft results including unassigned schools.
    /// </summary>
    [Function("GetDraftResults")]
    public async Task<HttpResponseData> GetDraftResults(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/draft-results")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("GetDraftResults called for auction {AuctionId}", auctionId);

        try
        {
            var draftPicks = await _context.DraftPicks
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Include(dp => dp.Team)
                .Include(dp => dp.RosterPosition)
                .Include(dp => dp.NominatedByUser)
                .Include(dp => dp.WonByUser)
                .Where(dp => dp.AuctionId == auctionId)
                .OrderBy(dp => dp.PickOrder)
                .ToListAsync();

            var result = draftPicks.Select(dp => new
            {
                DraftPickId = dp.DraftPickId,
                PickOrder = dp.PickOrder,
                SchoolId = dp.AuctionSchool.SchoolId,
                SchoolName = dp.AuctionSchool.School.Name,
                Conference = dp.AuctionSchool.Conference,
                LeagifyPosition = dp.AuctionSchool.LeagifyPosition,
                ProjectedPoints = dp.AuctionSchool.ProjectedPoints,
                TeamId = dp.TeamId,
                TeamName = dp.Team.TeamName,
                RosterPositionId = dp.RosterPositionId,
                RosterPositionName = dp.RosterPositionId > 0 ? dp.RosterPosition?.PositionName : "Unassigned",
                WinningBid = dp.WinningBid,
                NominatedBy = dp.NominatedByUser.DisplayName,
                WonBy = dp.WonByUser.DisplayName,
                DraftedDate = dp.DraftedDate,
                IsAssignmentConfirmed = dp.IsAssignmentConfirmed
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting draft results for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, "Failed to get draft results");
        }
    }

    /// <summary>
    /// Assigns a draft pick to a specific roster position.
    /// Validates position eligibility and roster slot availability.
    /// </summary>
    [Function("AssignToRosterPosition")]
    public async Task<MultiResponse> AssignToRosterPosition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/draft-picks/{draftPickId}/assign")] HttpRequestData req,
        int auctionId,
        int draftPickId)
    {
        _logger.LogInformation("AssignToRosterPosition called for draft pick {DraftPickId}", draftPickId);

        try
        {
            // Verify authentication
            var sessionToken = GetSessionToken(req);
            if (string.IsNullOrEmpty(sessionToken))
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Team)
                .FirstOrDefaultAsync(u => u.SessionToken == sessionToken && u.AuctionId == auctionId);

            if (user == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            // Parse request
            var requestBody = await req.ReadAsStringAsync();
            var assignmentRequest = JsonSerializer.Deserialize<RosterAssignmentRequest>(requestBody ?? "{}");

            if (assignmentRequest?.RosterPositionId == null || assignmentRequest.RosterPositionId <= 0)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "Valid roster position ID required")
                };
            }

            // Get draft pick
            var draftPick = await _context.DraftPicks
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Include(dp => dp.Team)
                .FirstOrDefaultAsync(dp => dp.DraftPickId == draftPickId && dp.AuctionId == auctionId);

            if (draftPick == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Draft pick not found")
                };
            }

            // Get user's team
            var userTeam = user.UserRoles
                .Where(ur => ur.TeamId != null && ur.Team != null)
                .OrderBy(ur => ur.Role == "TeamCoach" ? 0 : 1)
                .FirstOrDefault()?.Team;

            // Verify user owns this draft pick (either directly or as team member)
            if (userTeam == null || userTeam.TeamId != draftPick.TeamId)
            {
                // Check if user is auction master
                var isAuctionMaster = user.UserRoles
                    .Any(ur => ur.Role == "AuctionMaster");

                if (!isAuctionMaster)
                {
                    return new MultiResponse
                    {
                        HttpResponse = await CreateUnauthorizedResponse(req)
                    };
                }
            }

            // Get roster position
            var rosterPosition = await _context.RosterPositions
                .FirstOrDefaultAsync(rp => rp.RosterPositionId == assignmentRequest.RosterPositionId && rp.AuctionId == auctionId);

            if (rosterPosition == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Roster position not found")
                };
            }

            // Validate school eligibility for position
            if (!rosterPosition.IsFlexPosition)
            {
                // Check if school's LeagifyPosition matches roster position name
                if (!string.Equals(draftPick.AuctionSchool.LeagifyPosition, rosterPosition.PositionName, StringComparison.OrdinalIgnoreCase))
                {
                    return new MultiResponse
                    {
                        HttpResponse = await CreateBadRequestResponse(req,
                            $"School's position ({draftPick.AuctionSchool.LeagifyPosition}) does not match roster position ({rosterPosition.PositionName})")
                    };
                }
            }

            // Check if team has available slots for this position
            var currentAssignments = await _context.DraftPicks
                .CountAsync(dp => dp.TeamId == draftPick.TeamId &&
                                 dp.RosterPositionId == rosterPosition.RosterPositionId &&
                                 dp.DraftPickId != draftPickId); // Exclude current pick in case of reassignment

            if (currentAssignments >= rosterPosition.SlotsPerTeam)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req,
                        $"Team has no available slots for position {rosterPosition.PositionName} (limit: {rosterPosition.SlotsPerTeam})")
                };
            }

            // Assign to position
            draftPick.RosterPositionId = rosterPosition.RosterPositionId;
            draftPick.IsAssignmentConfirmed = true;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned draft pick {DraftPickId} to roster position {RosterPositionId}",
                draftPickId, rosterPosition.RosterPositionId);

            // Check if auction is complete
            await CheckAuctionCompletion(auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "School assigned to roster position",
                DraftPickId = draftPick.DraftPickId,
                SchoolName = draftPick.AuctionSchool.School.Name,
                RosterPositionName = rosterPosition.PositionName,
                TeamName = draftPick.Team.TeamName
            });

            // Broadcast assignment
            var signalRMessages = new[]
            {
                new SignalRMessageAction
                {
                    Target = "RosterAssignmentUpdated",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            DraftPickId = draftPick.DraftPickId,
                            TeamId = draftPick.TeamId,
                            TeamName = draftPick.Team.TeamName,
                            SchoolName = draftPick.AuctionSchool.School.Name,
                            RosterPositionName = rosterPosition.PositionName
                        }
                    }
                }
            };

            return new MultiResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning draft pick {DraftPickId} to roster position", draftPickId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to assign roster position")
            };
        }
    }

    /// <summary>
    /// Auto-assigns a draft pick to the most restrictive valid roster position.
    /// Prefers specific positions over flex positions.
    /// </summary>
    [Function("AutoAssignRosterPosition")]
    public async Task<MultiResponse> AutoAssignRosterPosition(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auction/{auctionId}/draft-picks/{draftPickId}/auto-assign")] HttpRequestData req,
        int auctionId,
        int draftPickId)
    {
        _logger.LogInformation("AutoAssignRosterPosition called for draft pick {DraftPickId}", draftPickId);

        try
        {
            // Verify authentication (allow both user and management tokens)
            var sessionToken = GetSessionToken(req);
            var managementToken = GetManagementToken(req);

            if (string.IsNullOrEmpty(sessionToken) && string.IsNullOrEmpty(managementToken))
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateUnauthorizedResponse(req)
                };
            }

            // Get draft pick
            var draftPick = await _context.DraftPicks
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Include(dp => dp.Team)
                .FirstOrDefaultAsync(dp => dp.DraftPickId == draftPickId && dp.AuctionId == auctionId);

            if (draftPick == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateNotFoundResponse(req, "Draft pick not found")
                };
            }

            // Get all roster positions for auction
            var rosterPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .OrderBy(rp => rp.IsFlexPosition) // Specific positions first, then flex
                .ThenBy(rp => rp.DisplayOrder)
                .ToListAsync();

            // Get current team assignments
            var currentAssignments = await _context.DraftPicks
                .Where(dp => dp.TeamId == draftPick.TeamId && dp.DraftPickId != draftPickId)
                .GroupBy(dp => dp.RosterPositionId)
                .Select(g => new { RosterPositionId = g.Key, Count = g.Count() })
                .ToListAsync();

            var assignmentCounts = currentAssignments.ToDictionary(x => x.RosterPositionId, x => x.Count);

            // Find best position (most restrictive with available slots)
            RosterPosition? bestPosition = null;

            foreach (var position in rosterPositions)
            {
                // Check if position has available slots
                var currentCount = assignmentCounts.GetValueOrDefault(position.RosterPositionId, 0);
                if (currentCount >= position.SlotsPerTeam)
                {
                    continue; // Position is full
                }

                // Check eligibility
                if (position.IsFlexPosition)
                {
                    // Flex position accepts any school
                    bestPosition = position;
                    break; // Use first available flex position
                }
                else
                {
                    // Specific position requires matching LeagifyPosition
                    if (string.Equals(draftPick.AuctionSchool.LeagifyPosition, position.PositionName, StringComparison.OrdinalIgnoreCase))
                    {
                        bestPosition = position;
                        break; // Found most restrictive match
                    }
                }
            }

            if (bestPosition == null)
            {
                return new MultiResponse
                {
                    HttpResponse = await CreateBadRequestResponse(req, "No available roster positions for this school")
                };
            }

            // Assign to best position
            draftPick.RosterPositionId = bestPosition.RosterPositionId;
            draftPick.IsAssignmentConfirmed = true;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Auto-assigned draft pick {DraftPickId} to roster position {RosterPositionId}",
                draftPickId, bestPosition.RosterPositionId);

            // Check if auction is complete
            await CheckAuctionCompletion(auctionId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Message = "School auto-assigned to roster position",
                DraftPickId = draftPick.DraftPickId,
                SchoolName = draftPick.AuctionSchool.School.Name,
                RosterPositionName = bestPosition.PositionName,
                TeamName = draftPick.Team.TeamName,
                IsFlexPosition = bestPosition.IsFlexPosition
            });

            // Broadcast assignment
            var signalRMessages = new[]
            {
                new SignalRMessageAction
                {
                    Target = "RosterAssignmentUpdated",
                    GroupName = $"auction-{auctionId}",
                    Arguments = new object[]
                    {
                        new
                        {
                            DraftPickId = draftPick.DraftPickId,
                            TeamId = draftPick.TeamId,
                            TeamName = draftPick.Team.TeamName,
                            SchoolName = draftPick.AuctionSchool.School.Name,
                            RosterPositionName = bestPosition.PositionName,
                            IsAutoAssigned = true
                        }
                    }
                }
            };

            return new MultiResponse
            {
                HttpResponse = response,
                SignalRMessages = signalRMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-assigning draft pick {DraftPickId}", draftPickId);
            return new MultiResponse
            {
                HttpResponse = await CreateErrorResponse(req, "Failed to auto-assign roster position")
            };
        }
    }

    /// <summary>
    /// Gets team roster with all assigned schools grouped by position.
    /// Shows remaining slots and budget for each team.
    /// </summary>
    [Function("GetTeamRoster")]
    public async Task<HttpResponseData> GetTeamRoster(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/teams/{teamId}/roster")] HttpRequestData req,
        int auctionId,
        int teamId)
    {
        _logger.LogInformation("GetTeamRoster called for team {TeamId} in auction {AuctionId}", teamId, auctionId);

        try
        {
            var team = await _context.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamId && t.AuctionId == auctionId);

            if (team == null)
            {
                return await CreateNotFoundResponse(req, "Team not found");
            }

            var rosterPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .OrderBy(rp => rp.DisplayOrder)
                .ToListAsync();

            var draftPicks = await _context.DraftPicks
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Include(dp => dp.RosterPosition)
                .Where(dp => dp.TeamId == teamId)
                .ToListAsync();

            var rosterSlots = rosterPositions.Select(rp => new
            {
                RosterPositionId = rp.RosterPositionId,
                PositionName = rp.PositionName,
                SlotsPerTeam = rp.SlotsPerTeam,
                IsFlexPosition = rp.IsFlexPosition,
                ColorCode = rp.ColorCode,
                AssignedSchools = draftPicks
                    .Where(dp => dp.RosterPositionId == rp.RosterPositionId)
                    .Select(dp => new
                    {
                        DraftPickId = dp.DraftPickId,
                        SchoolName = dp.AuctionSchool.School.Name,
                        Conference = dp.AuctionSchool.Conference,
                        ProjectedPoints = dp.AuctionSchool.ProjectedPoints,
                        WinningBid = dp.WinningBid,
                        IsAssignmentConfirmed = dp.IsAssignmentConfirmed
                    })
                    .ToList(),
                FilledSlots = draftPicks.Count(dp => dp.RosterPositionId == rp.RosterPositionId),
                RemainingSlots = rp.SlotsPerTeam - draftPicks.Count(dp => dp.RosterPositionId == rp.RosterPositionId)
            }).ToList();

            var result = new
            {
                TeamId = team.TeamId,
                TeamName = team.TeamName,
                CurrentBudget = team.RemainingBudget,
                TotalSlots = rosterPositions.Sum(rp => rp.SlotsPerTeam),
                FilledSlots = draftPicks.Count(dp => dp.IsAssignmentConfirmed),
                UnassignedPicks = draftPicks.Count(dp => !dp.IsAssignmentConfirmed || dp.RosterPositionId == 0),
                TotalProjectedPoints = draftPicks.Sum(dp => dp.AuctionSchool.ProjectedPoints),
                TotalSpent = draftPicks.Sum(dp => dp.WinningBid),
                RosterSlots = rosterSlots
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team roster for team {TeamId}", teamId);
            return await CreateErrorResponse(req, "Failed to get team roster");
        }
    }

    // Helper methods

    private async Task CheckAuctionCompletion(int auctionId)
    {
        try
        {
            // Get total roster slots needed
            var totalSlotsNeeded = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .SumAsync(rp => rp.SlotsPerTeam);

            var teamCount = await _context.Teams
                .CountAsync(t => t.AuctionId == auctionId);

            var totalSlotsForAuction = totalSlotsNeeded * teamCount;

            // Count assigned draft picks
            var assignedPicks = await _context.DraftPicks
                .CountAsync(dp => dp.AuctionId == auctionId && dp.IsAssignmentConfirmed && dp.RosterPositionId > 0);

            // If all slots filled, mark auction as complete
            if (assignedPicks >= totalSlotsForAuction)
            {
                var auction = await _context.Auctions.FindAsync(auctionId);
                if (auction != null && auction.Status == "InProgress")
                {
                    auction.Status = "Completed";
                    auction.CompletedDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Auction {AuctionId} marked as completed", auctionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking auction completion for auction {AuctionId}", auctionId);
        }
    }

    private static string? GetSessionToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Auction-Token", out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
    }

    private static string? GetManagementToken(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Management-Token", out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
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

    // DTOs

    private class RosterAssignmentRequest
    {
        public int RosterPositionId { get; set; }
    }

    // SignalR support classes

    public class MultiResponse
    {
        public HttpResponseData? HttpResponse { get; set; }
        public SignalRMessageAction[]? SignalRMessages { get; set; }
    }

    public class SignalRMessageAction
    {
        public string Target { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public object[] Arguments { get; set; } = Array.Empty<object>();
    }
}
