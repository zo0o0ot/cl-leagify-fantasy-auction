using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Services;

/// <summary>
/// Service for managing real-time bidding operations during an auction.
/// Handles bid completion detection, school awards, and turn advancement.
/// </summary>
public class BiddingService : IBiddingService
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<BiddingService> _logger;

    public BiddingService(LeagifyAuctionDbContext context, ILogger<BiddingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BiddingStatusResult> CheckBiddingStatusAsync(int auctionId)
    {
        var result = new BiddingStatusResult();

        try
        {
            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                result.ErrorMessage = "Auction not found";
                return result;
            }

            if (auction.CurrentSchoolId == null)
            {
                result.ErrorMessage = "No active bidding";
                return result;
            }

            result.HighBidderUserId = auction.CurrentHighBidderUserId;
            result.CurrentHighBid = auction.CurrentHighBid;

            // Get all users who have teams (can participate in bidding)
            var usersWithTeams = await _context.UserRoles
                .Include(ur => ur.User)
                .Include(ur => ur.Team)
                .Where(ur => ur.User.AuctionId == auctionId &&
                             ur.TeamId != null &&
                             ur.Team != null &&
                             (ur.Role == "TeamCoach" || ur.Role == "ProxyCoach"))
                .ToListAsync();

            // Get passes for the current school
            var passes = await _context.BidHistories
                .Where(bh => bh.AuctionId == auctionId &&
                             bh.AuctionSchoolId == auction.CurrentSchoolId &&
                             bh.BidType == "Pass")
                .Select(bh => bh.UserId)
                .Distinct()
                .ToListAsync();

            result.PassedUserIds = passes;

            // Get roster info to calculate remaining slots per team
            var rosterPositions = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .ToListAsync();

            var totalSlotsPerTeam = rosterPositions.Sum(rp => rp.SlotsPerTeam);

            var draftPicksByTeam = await _context.DraftPicks
                .Where(dp => dp.AuctionId == auctionId)
                .GroupBy(dp => dp.TeamId)
                .Select(g => new { TeamId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TeamId, x => x.Count);

            // Build eligible bidders list
            // Group by team to avoid duplicate team entries (a user might have multiple roles)
            var teamUsers = usersWithTeams
                .GroupBy(ur => ur.TeamId)
                .Select(g => new
                {
                    TeamId = g.Key!.Value,
                    Team = g.First().Team!,
                    // Prefer TeamCoach, otherwise take first ProxyCoach
                    User = g.OrderBy(ur => ur.Role == "TeamCoach" ? 0 : 1).First().User
                })
                .ToList();

            foreach (var tu in teamUsers)
            {
                var team = tu.Team;
                var user = tu.User;

                // Calculate remaining slots for this team
                var currentPicks = draftPicksByTeam.GetValueOrDefault(team.TeamId, 0);
                var remainingSlots = totalSlotsPerTeam - currentPicks;

                // Skip teams with full rosters
                if (remainingSlots <= 0)
                {
                    _logger.LogDebug("Team {TeamId} has full roster, skipping", team.TeamId);
                    continue;
                }

                // Calculate max bid: Budget - (RemainingSlots - 1)
                // Must reserve $1 for each remaining slot after this one
                var maxBid = team.RemainingBudget - (remainingSlots - 1);

                var hasPassed = passes.Contains(user.UserId);
                var isAutoPassed = maxBid <= (auction.CurrentHighBid ?? 0);
                var isHighBidder = user.UserId == auction.CurrentHighBidderUserId;

                var bidder = new EligibleBidder
                {
                    UserId = user.UserId,
                    DisplayName = user.DisplayName,
                    TeamId = team.TeamId,
                    TeamName = team.TeamName ?? $"Team {team.TeamId}",
                    MaxBid = maxBid,
                    HasPassed = hasPassed,
                    IsAutoPassed = isAutoPassed
                };

                result.EligibleBidders.Add(bidder);

                if (isAutoPassed && !isHighBidder)
                {
                    result.AutoPassedUserIds.Add(user.UserId);
                }
            }

            // Determine if bidding should end
            // Bidding ends when all non-high-bidders have either passed or are auto-passed
            var nonHighBidders = result.EligibleBidders
                .Where(b => b.UserId != auction.CurrentHighBidderUserId)
                .ToList();

            if (nonHighBidders.Count == 0)
            {
                // Only the high bidder is eligible (nominator wins by default)
                result.ShouldEndBidding = true;
                _logger.LogInformation("Auction {AuctionId}: Only high bidder eligible, bidding should end", auctionId);
            }
            else
            {
                var allOthersOut = nonHighBidders.All(b => b.HasPassed || b.IsAutoPassed);
                result.ShouldEndBidding = allOthersOut;

                if (allOthersOut)
                {
                    _logger.LogInformation(
                        "Auction {AuctionId}: All {Count} non-high-bidders have passed or can't afford, bidding should end",
                        auctionId, nonHighBidders.Count);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking bidding status for auction {AuctionId}", auctionId);
            result.ErrorMessage = "Error checking bidding status";
            return result;
        }
    }

    /// <inheritdoc />
    public async Task<CompleteBiddingResult> CompleteBiddingAsync(int auctionId)
    {
        var result = new CompleteBiddingResult();

        try
        {
            var auction = await _context.Auctions
                .Include(a => a.CurrentSchool)
                    .ThenInclude(s => s != null ? s.School : null)
                .Include(a => a.CurrentHighBidderUser)
                    .ThenInclude(u => u != null ? u.UserRoles : null!)
                        .ThenInclude(ur => ur.Team)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
            {
                result.ErrorMessage = "Auction not found";
                return result;
            }

            if (auction.CurrentSchoolId == null || auction.CurrentHighBidderUserId == null)
            {
                result.ErrorMessage = "No active bidding to complete";
                return result;
            }

            var winningUser = auction.CurrentHighBidderUser!;

            // Get winning user's team
            var winningTeam = winningUser.UserRoles
                .Where(ur => ur.TeamId != null && ur.Team != null)
                .OrderBy(ur => ur.Role == "TeamCoach" ? 0 : 1)
                .FirstOrDefault()?.Team;

            if (winningTeam == null)
            {
                result.ErrorMessage = "Winning bidder must be assigned to a team";
                return result;
            }

            var schoolName = auction.CurrentSchool?.School?.Name ?? "Unknown School";
            var winningBid = auction.CurrentHighBid!.Value;

            // Create draft pick record
            var pickOrder = await _context.DraftPicks.CountAsync(dp => dp.AuctionId == auctionId) + 1;

            var draftPick = new DraftPick
            {
                AuctionId = auctionId,
                TeamId = winningTeam.TeamId,
                AuctionSchoolId = auction.CurrentSchoolId.Value,
                RosterPositionId = 0, // Will be assigned later by user
                WinningBid = winningBid,
                NominatedByUserId = auction.CurrentNominatorUserId ?? winningUser.UserId,
                WonByUserId = winningUser.UserId,
                PickOrder = pickOrder,
                DraftedDate = DateTime.UtcNow,
                IsAssignmentConfirmed = false
            };

            _context.DraftPicks.Add(draftPick);

            // Deduct from team budget
            winningTeam.RemainingBudget -= winningBid;

            // Mark winning bid in history
            var winningBidHistory = await _context.BidHistories
                .Where(bh => bh.AuctionId == auctionId &&
                             bh.AuctionSchoolId == auction.CurrentSchoolId &&
                             bh.UserId == winningUser.UserId &&
                             bh.BidAmount == winningBid)
                .OrderByDescending(bh => bh.BidDate)
                .FirstOrDefaultAsync();

            if (winningBidHistory != null)
            {
                winningBidHistory.IsWinningBid = true;
            }

            // Clear current bidding state
            auction.CurrentSchoolId = null;
            auction.CurrentHighBid = null;
            auction.CurrentHighBidderUserId = null;
            auction.ModifiedDate = DateTime.UtcNow;

            // Advance to next nominator
            var nextNominator = await AdvanceNominatorAsync(auctionId, auction);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Completed bidding in auction {AuctionId}: {SchoolName} won by {WinnerName} ({TeamName}) for ${Amount}",
                auctionId, schoolName, winningUser.DisplayName, winningTeam.TeamName, winningBid);

            // Check if auction is complete
            var isComplete = await CheckAuctionCompleteAsync(auctionId);

            result.Success = true;
            result.DraftPickId = draftPick.DraftPickId;
            result.SchoolName = schoolName;
            result.WinningBid = winningBid;
            result.WinnerUserId = winningUser.UserId;
            result.WinnerDisplayName = winningUser.DisplayName;
            result.TeamId = winningTeam.TeamId;
            result.TeamName = winningTeam.TeamName ?? $"Team {winningTeam.TeamId}";
            result.NextNominatorUserId = nextNominator?.UserId;
            result.NextNominatorDisplayName = nextNominator?.DisplayName;
            result.IsAuctionComplete = isComplete;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing bidding for auction {AuctionId}", auctionId);
            result.ErrorMessage = "Error completing bidding";
            return result;
        }
    }

    /// <inheritdoc />
    public async Task<decimal> GetMaxBidForUserAsync(int auctionId, int userId)
    {
        try
        {
            // Get user's team
            var userRole = await _context.UserRoles
                .Include(ur => ur.Team)
                .Include(ur => ur.User)
                .Where(ur => ur.User.AuctionId == auctionId &&
                             ur.UserId == userId &&
                             ur.TeamId != null &&
                             (ur.Role == "TeamCoach" || ur.Role == "ProxyCoach"))
                .OrderBy(ur => ur.Role == "TeamCoach" ? 0 : 1)
                .FirstOrDefaultAsync();

            if (userRole?.Team == null)
            {
                return 0;
            }

            var team = userRole.Team;

            // Get roster info
            var totalSlots = await _context.RosterPositions
                .Where(rp => rp.AuctionId == auctionId)
                .SumAsync(rp => rp.SlotsPerTeam);

            var currentPicks = await _context.DraftPicks
                .CountAsync(dp => dp.TeamId == team.TeamId);

            var remainingSlots = totalSlots - currentPicks;

            if (remainingSlots <= 0)
            {
                return 0;
            }

            // MaxBid = Budget - (RemainingSlots - 1)
            return team.RemainingBudget - (remainingSlots - 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting max bid for user {UserId} in auction {AuctionId}", userId, auctionId);
            return 0;
        }
    }

    /// <summary>
    /// Advances to the next nominator after a school is awarded.
    /// </summary>
    private async Task<User?> AdvanceNominatorAsync(int auctionId, Auction auction)
    {
        var nominationOrders = await _context.NominationOrders
            .Include(no => no.User)
            .Where(no => no.AuctionId == auctionId)
            .OrderBy(no => no.OrderPosition)
            .ToListAsync();

        if (!nominationOrders.Any())
        {
            _logger.LogWarning("No nomination order configured for auction {AuctionId}", auctionId);
            auction.CurrentNominatorUserId = null;
            return null;
        }

        // Mark current nominator as having nominated
        if (auction.CurrentNominatorUserId != null)
        {
            var currentOrder = nominationOrders
                .FirstOrDefault(no => no.UserId == auction.CurrentNominatorUserId);
            if (currentOrder != null)
            {
                currentOrder.HasNominated = true;
            }
        }

        // Get roster info to check who can still nominate
        var totalSlotsPerTeam = await _context.RosterPositions
            .Where(rp => rp.AuctionId == auctionId)
            .SumAsync(rp => rp.SlotsPerTeam);

        var draftPicksByTeam = await _context.DraftPicks
            .Where(dp => dp.AuctionId == auctionId)
            .GroupBy(dp => dp.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count);

        // Get team assignments for users
        var userTeams = await _context.UserRoles
            .Include(ur => ur.User)
            .Where(ur => ur.User.AuctionId == auctionId && ur.TeamId != null)
            .ToDictionaryAsync(ur => ur.UserId, ur => ur.TeamId!.Value);

        // Find next eligible nominator
        NominationOrder? nextNominator = null;

        // First, try to find someone who hasn't nominated yet and can still nominate
        foreach (var no in nominationOrders.Where(n => !n.HasNominated && !n.IsSkipped))
        {
            if (CanUserNominate(no.UserId, userTeams, draftPicksByTeam, totalSlotsPerTeam))
            {
                nextNominator = no;
                break;
            }
            else
            {
                // Mark as skipped if roster is full
                no.IsSkipped = true;
            }
        }

        // If everyone has nominated, reset for next round
        if (nextNominator == null)
        {
            foreach (var no in nominationOrders)
            {
                no.HasNominated = false;
            }

            foreach (var no in nominationOrders.Where(n => !n.IsSkipped))
            {
                if (CanUserNominate(no.UserId, userTeams, draftPicksByTeam, totalSlotsPerTeam))
                {
                    nextNominator = no;
                    break;
                }
                else
                {
                    no.IsSkipped = true;
                }
            }
        }

        if (nextNominator != null)
        {
            auction.CurrentNominatorUserId = nextNominator.UserId;
            _logger.LogInformation("Advanced nomination to user {UserId} ({DisplayName}) in auction {AuctionId}",
                nextNominator.UserId, nextNominator.User.DisplayName, auctionId);
            return nextNominator.User;
        }
        else
        {
            // No valid nominators - auction may be complete
            auction.CurrentNominatorUserId = null;
            _logger.LogInformation("No eligible nominators remaining in auction {AuctionId}", auctionId);
            return null;
        }
    }

    /// <summary>
    /// Checks if a user can nominate (has team with open roster slots).
    /// </summary>
    private bool CanUserNominate(
        int userId,
        Dictionary<int, int> userTeams,
        Dictionary<int, int> draftPicksByTeam,
        int totalSlotsPerTeam)
    {
        if (!userTeams.TryGetValue(userId, out var teamId))
        {
            return false;
        }

        var currentPicks = draftPicksByTeam.GetValueOrDefault(teamId, 0);
        return currentPicks < totalSlotsPerTeam;
    }

    /// <summary>
    /// Checks if the auction is complete (all team rosters are full).
    /// </summary>
    private async Task<bool> CheckAuctionCompleteAsync(int auctionId)
    {
        var totalSlotsPerTeam = await _context.RosterPositions
            .Where(rp => rp.AuctionId == auctionId)
            .SumAsync(rp => rp.SlotsPerTeam);

        var teamCount = await _context.Teams
            .CountAsync(t => t.AuctionId == auctionId);

        var totalSlotsNeeded = totalSlotsPerTeam * teamCount;

        var totalPicks = await _context.DraftPicks
            .CountAsync(dp => dp.AuctionId == auctionId);

        if (totalPicks >= totalSlotsNeeded)
        {
            var auction = await _context.Auctions.FindAsync(auctionId);
            if (auction != null && auction.Status == "InProgress")
            {
                auction.Status = "Completed";
                auction.CompletedDate = DateTime.UtcNow;
                _logger.LogInformation("Auction {AuctionId} marked as completed", auctionId);
            }
            return true;
        }

        return false;
    }
}
