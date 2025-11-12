using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class User
{
    public int UserId { get; set; }
    
    public int AuctionId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? ConnectionId { get; set; }
    
    [MaxLength(200)]
    public string? SessionToken { get; set; }
    
    public bool IsConnected { get; set; } = false;
    
    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    
    public DateTime LastActiveDate { get; set; } = DateTime.UtcNow;
    
    public bool IsReconnectionPending { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the user has successfully placed at least one test bid in the waiting room.
    /// </summary>
    /// <value>True if the user has tested the bidding interface; false otherwise.</value>
    /// <remarks>
    /// This represents the technical readiness indicator (✅) in the waiting room.
    /// Automatically set to true when the user places their first test bid on Vermont A&M.
    /// </remarks>
    public bool HasTestedBidding { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the user has manually confirmed they are ready to start the live auction.
    /// </summary>
    /// <value>True if the user clicked "I'm Ready to Draft"; false if still testing or not ready.</value>
    /// <remarks>
    /// This represents the personal readiness indicator (🏈) in the waiting room.
    /// Must be manually toggled by the user via the "I'm Ready to Draft" button.
    /// Can be toggled back to false if user needs more time.
    /// </remarks>
    public bool IsReadyToDraft { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the user has passed on the current test bid in the waiting room.
    /// </summary>
    /// <value>True if the user clicked "Pass" on the current test bidding round; false if still actively bidding.</value>
    /// <remarks>
    /// This flag is reset to false whenever a new test bid is placed by anyone.
    /// Used to show which participants are actively bidding vs. who has passed.
    /// </remarks>
    public bool HasPassedOnTestBid { get; set; } = false;

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    public virtual ICollection<DraftPick> NominatedPicks { get; set; } = new List<DraftPick>();
    public virtual ICollection<DraftPick> WonPicks { get; set; } = new List<DraftPick>();
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();
    public virtual ICollection<NominationOrder> NominationOrders { get; set; } = new List<NominationOrder>();
}