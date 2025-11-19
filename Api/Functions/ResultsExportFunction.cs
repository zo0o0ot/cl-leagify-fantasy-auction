using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using System.Net;
using System.Text;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Handles export of auction results to CSV format.
/// Generates downloadable draft results matching the SampleFantasyDraft.csv format.
/// </summary>
public class ResultsExportFunction(LeagifyAuctionDbContext context, ILogger<ResultsExportFunction> logger)
{
    private readonly LeagifyAuctionDbContext _context = context;
    private readonly ILogger<ResultsExportFunction> _logger = logger;

    /// <summary>
    /// Exports complete draft results to CSV format.
    /// Format: Owner,Player,Position,Bid,ProjectedPoints
    /// Returns downloadable CSV file.
    /// </summary>
    [Function("ExportDraftResults")]
    public async Task<HttpResponseData> ExportDraftResults(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/export")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("ExportDraftResults called for auction {AuctionId}", auctionId);

        try
        {
            // Get auction details
            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return await CreateNotFoundResponse(req, "Auction not found");
            }

            // Get all draft picks with related data
            var draftPicks = await _context.DraftPicks
                .Include(dp => dp.Team)
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Include(dp => dp.RosterPosition)
                .Where(dp => dp.AuctionId == auctionId)
                .OrderBy(dp => dp.PickOrder)
                .ToListAsync();

            if (!draftPicks.Any())
            {
                return await CreateBadRequestResponse(req, "No draft results available for this auction");
            }

            // Build CSV content
            var csvBuilder = new StringBuilder();

            // Header row
            csvBuilder.AppendLine("Owner,Player,Position,Bid,ProjectedPoints");

            // Data rows
            foreach (var pick in draftPicks)
            {
                var owner = EscapeCsvField(pick.Team.TeamName);
                var player = EscapeCsvField(pick.AuctionSchool.School.Name);
                var position = pick.RosterPositionId > 0 && pick.RosterPosition != null
                    ? EscapeCsvField(pick.RosterPosition.PositionName)
                    : "Unassigned";
                var bid = pick.WinningBid.ToString("0");
                var projectedPoints = pick.AuctionSchool.ProjectedPoints.ToString("0");

                csvBuilder.AppendLine($"{owner},{player},{position},{bid},{projectedPoints}");
            }

            var csvContent = csvBuilder.ToString();

            // Create response with CSV content
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/csv; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{GenerateFilename(auction)}\"");
            await response.WriteStringAsync(csvContent);

            _logger.LogInformation("Exported {Count} draft picks for auction {AuctionId}", draftPicks.Count, auctionId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting draft results for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, "Failed to export draft results");
        }
    }

    /// <summary>
    /// Exports team-specific roster results.
    /// Same format but filtered to single team's picks.
    /// </summary>
    [Function("ExportTeamRoster")]
    public async Task<HttpResponseData> ExportTeamRoster(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/teams/{teamId}/export")] HttpRequestData req,
        int auctionId,
        int teamId)
    {
        _logger.LogInformation("ExportTeamRoster called for team {TeamId} in auction {AuctionId}", teamId, auctionId);

        try
        {
            // Verify team exists
            var team = await _context.Teams
                .Include(t => t.Auction)
                .FirstOrDefaultAsync(t => t.TeamId == teamId && t.AuctionId == auctionId);

            if (team == null)
            {
                return await CreateNotFoundResponse(req, "Team not found");
            }

            // Get team's draft picks
            var draftPicks = await _context.DraftPicks
                .Include(dp => dp.Team)
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Include(dp => dp.RosterPosition)
                .Where(dp => dp.TeamId == teamId)
                .OrderBy(dp => dp.RosterPosition != null ? dp.RosterPosition.DisplayOrder : 999)
                .ThenBy(dp => dp.PickOrder)
                .ToListAsync();

            if (!draftPicks.Any())
            {
                return await CreateBadRequestResponse(req, "No draft picks available for this team");
            }

            // Build CSV content
            var csvBuilder = new StringBuilder();

            // Header row
            csvBuilder.AppendLine("Owner,Player,Position,Bid,ProjectedPoints");

            // Data rows
            foreach (var pick in draftPicks)
            {
                var owner = EscapeCsvField(pick.Team.TeamName);
                var player = EscapeCsvField(pick.AuctionSchool.School.Name);
                var position = pick.RosterPositionId > 0 && pick.RosterPosition != null
                    ? EscapeCsvField(pick.RosterPosition.PositionName)
                    : "Unassigned";
                var bid = pick.WinningBid.ToString("0");
                var projectedPoints = pick.AuctionSchool.ProjectedPoints.ToString("0");

                csvBuilder.AppendLine($"{owner},{player},{position},{bid},{projectedPoints}");
            }

            // Add summary rows
            csvBuilder.AppendLine();
            csvBuilder.AppendLine($"Total Spent,{draftPicks.Sum(dp => dp.WinningBid):0}");
            csvBuilder.AppendLine($"Total Projected Points,{draftPicks.Sum(dp => dp.AuctionSchool.ProjectedPoints):0}");
            csvBuilder.AppendLine($"Remaining Budget,{team.RemainingBudget:0}");

            var csvContent = csvBuilder.ToString();

            // Create response with CSV content
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/csv; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{GenerateTeamFilename(team)}\"");
            await response.WriteStringAsync(csvContent);

            _logger.LogInformation("Exported {Count} draft picks for team {TeamId}", draftPicks.Count, teamId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting team roster for team {TeamId}", teamId);
            return await CreateErrorResponse(req, "Failed to export team roster");
        }
    }

    /// <summary>
    /// Gets auction summary statistics for display.
    /// Includes team standings, top bids, and completion status.
    /// </summary>
    [Function("GetAuctionSummary")]
    public async Task<HttpResponseData> GetAuctionSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auction/{auctionId}/summary")] HttpRequestData req,
        int auctionId)
    {
        _logger.LogInformation("GetAuctionSummary called for auction {AuctionId}", auctionId);

        try
        {
            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                return await CreateNotFoundResponse(req, "Auction not found");
            }

            // Get team standings
            var teams = await _context.Teams
                .Where(t => t.AuctionId == auctionId)
                .Select(t => new
                {
                    t.TeamId,
                    t.TeamName,
                    t.RemainingBudget,
                    t.Budget,
                    DraftPicks = _context.DraftPicks
                        .Include(dp => dp.AuctionSchool)
                        .Where(dp => dp.TeamId == t.TeamId)
                        .ToList()
                })
                .ToListAsync();

            var teamStandings = teams.Select(t => new
            {
                t.TeamId,
                Name = t.TeamName,
                CurrentBudget = t.RemainingBudget,
                TotalSpent = t.Budget - t.RemainingBudget,
                TotalProjectedPoints = t.DraftPicks.Sum(dp => dp.AuctionSchool.ProjectedPoints),
                SchoolsDrafted = t.DraftPicks.Count,
                AssignedSchools = t.DraftPicks.Count(dp => dp.IsAssignmentConfirmed && dp.RosterPositionId > 0)
            })
            .OrderByDescending(t => t.TotalProjectedPoints)
            .ToList();

            // Get top bids
            var topBids = await _context.DraftPicks
                .Include(dp => dp.Team)
                .Include(dp => dp.AuctionSchool)
                    .ThenInclude(asign => asign.School)
                .Where(dp => dp.AuctionId == auctionId)
                .OrderByDescending(dp => dp.WinningBid)
                .Take(10)
                .Select(dp => new
                {
                    SchoolName = dp.AuctionSchool.School.Name,
                    TeamName = dp.Team.TeamName,
                    WinningBid = dp.WinningBid,
                    ProjectedPoints = dp.AuctionSchool.ProjectedPoints
                })
                .ToListAsync();

            // Get roster positions summary
            var rosterPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .Select(rp => new
                {
                    rp.PositionName,
                    rp.SlotsPerTeam,
                    rp.IsFlexPosition
                })
                .ToListAsync();

            var totalSlots = rosterPositions.Sum(rp => rp.SlotsPerTeam) * teams.Count;
            var filledSlots = await _context.DraftPicks
                .CountAsync(dp => dp.AuctionId == auctionId && dp.IsAssignmentConfirmed && dp.RosterPositionId > 0);

            var summary = new
            {
                AuctionId = auction.AuctionId,
                AuctionName = auction.Name,
                Status = auction.Status,
                StartedDate = auction.StartedDate,
                CompletedDate = auction.CompletedDate,
                TotalTeams = teams.Count,
                TotalSlots = totalSlots,
                FilledSlots = filledSlots,
                CompletionPercentage = totalSlots > 0 ? (decimal)filledSlots / totalSlots * 100 : 0,
                TeamStandings = teamStandings,
                TopBids = topBids,
                RosterPositions = rosterPositions
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(summary);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auction summary for auction {AuctionId}", auctionId);
            return await CreateErrorResponse(req, "Failed to get auction summary");
        }
    }

    // Helper methods

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string GenerateFilename(LeagifyFantasyAuction.Api.Models.Auction auction)
    {
        var safeName = string.Concat(auction.Name.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-'))
            .Replace(" ", "_");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{safeName}_Results_{timestamp}.csv";
    }

    private static string GenerateTeamFilename(LeagifyFantasyAuction.Api.Models.Team team)
    {
        var safeName = string.Concat(team.Name.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-'))
            .Replace(" ", "_");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{safeName}_Roster_{timestamp}.csv";
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
}
