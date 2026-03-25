namespace LeagifyFantasyAuction.Api.Services;

/// <summary>
/// Service interface for managing real-time bidding operations during an auction.
/// Handles bid completion detection, award logic, and turn advancement.
/// </summary>
public interface IBiddingService
{
    /// <summary>
    /// Checks if bidding should end based on passes and budget constraints.
    /// Bidding ends when all eligible bidders (except the high bidder) have either:
    /// - Explicitly passed on the current school
    /// - Cannot afford to outbid (auto-passed due to insufficient budget)
    /// </summary>
    /// <param name="auctionId">The auction to check.</param>
    /// <returns>
    /// A result indicating whether bidding should end, with details about eligible bidders
    /// and who has passed or been auto-passed.
    /// </returns>
    Task<BiddingStatusResult> CheckBiddingStatusAsync(int auctionId);

    /// <summary>
    /// Completes bidding on the current school and awards it to the high bidder.
    /// Creates a DraftPick, deducts budget, clears bidding state, and advances the turn.
    /// </summary>
    /// <param name="auctionId">The auction ID.</param>
    /// <returns>Result containing the created draft pick and next nominator info.</returns>
    Task<CompleteBiddingResult> CompleteBiddingAsync(int auctionId);

    /// <summary>
    /// Gets the maximum bid amount a user can place based on their budget and remaining roster slots.
    /// MaxBid = RemainingBudget - (EmptyRosterSlots - 1)
    /// </summary>
    /// <param name="auctionId">The auction ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The maximum bid amount, or 0 if user cannot bid.</returns>
    Task<decimal> GetMaxBidForUserAsync(int auctionId, int userId);
}

/// <summary>
/// Result of checking bidding status.
/// </summary>
public class BiddingStatusResult
{
    /// <summary>
    /// Whether bidding should end (all eligible bidders have passed or can't afford to bid).
    /// </summary>
    public bool ShouldEndBidding { get; set; }

    /// <summary>
    /// The current high bidder's user ID.
    /// </summary>
    public int? HighBidderUserId { get; set; }

    /// <summary>
    /// The current high bid amount.
    /// </summary>
    public decimal? CurrentHighBid { get; set; }

    /// <summary>
    /// Users who can still bid (have budget and haven't passed).
    /// </summary>
    public List<EligibleBidder> EligibleBidders { get; set; } = new();

    /// <summary>
    /// Users who have explicitly passed on this school.
    /// </summary>
    public List<int> PassedUserIds { get; set; } = new();

    /// <summary>
    /// Users who are auto-passed because they can't afford to outbid.
    /// </summary>
    public List<int> AutoPassedUserIds { get; set; } = new();

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about a user who can potentially bid.
/// </summary>
public class EligibleBidder
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal MaxBid { get; set; }
    public bool HasPassed { get; set; }
    public bool IsAutoPassed { get; set; }
}

/// <summary>
/// Result of completing bidding.
/// </summary>
public class CompleteBiddingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Draft pick info
    public int DraftPickId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public decimal WinningBid { get; set; }
    public int WinnerUserId { get; set; }
    public string WinnerDisplayName { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;

    // Next nominator info
    public int? NextNominatorUserId { get; set; }
    public string? NextNominatorDisplayName { get; set; }

    // Indicates if auction is complete (all rosters full)
    public bool IsAuctionComplete { get; set; }
}
