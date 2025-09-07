using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

/// <summary>
/// Represents an auction session in the Leagify Fantasy Auction system.
/// Auctions coordinate the drafting process where users bid on schools for their fantasy teams.
/// </summary>
/// <remarks>
/// Each auction has a unique join code that users enter to participate, and a master recovery code
/// for auction masters to regain control. Auctions progress through Draft, InProgress, Complete, and Archived states.
/// The auction tracks current bidding state including the nominated school and current high bid.
/// </remarks>
public class Auction
{
    /// <summary>
    /// Gets or sets the unique identifier for the auction.
    /// </summary>
    /// <value>The primary key used to identify this auction across the system.</value>
    public int AuctionId { get; set; }
    
    /// <summary>
    /// Gets or sets the join code that users enter to participate in the auction.
    /// </summary>
    /// <value>A unique 6-10 character alphanumeric code that users share to join the auction.</value>
    /// <remarks>
    /// This code is case-insensitive and must be unique across all active auctions.
    /// It is automatically generated when the auction is created.
    /// </remarks>
    [Required]
    [MaxLength(10)]
    public string JoinCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the master recovery code for auction administrators.
    /// </summary>
    /// <value>A unique recovery code that allows auction masters to regain control of the auction.</value>
    /// <remarks>
    /// This code is more complex than the join code and is used when the auction master
    /// needs to reconnect or troubleshoot the auction. It should be kept secure.
    /// </remarks>
    [Required]
    [MaxLength(20)]
    public string MasterRecoveryCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display name of the auction.
    /// </summary>
    /// <value>A human-readable name that describes the auction (e.g., "2024 NFL Draft League").</value>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the current status of the auction.
    /// </summary>
    /// <value>One of: "Draft", "InProgress", "Complete", or "Archived".</value>
    /// <remarks>
    /// Draft: Auction is being configured and is not yet open to participants.
    /// InProgress: Auction is actively running with bidding taking place.
    /// Complete: Auction has finished and all schools have been assigned.
    /// Archived: Auction is complete and has been archived for historical purposes.
    /// </remarks>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, InProgress, Complete, Archived
    
    /// <summary>
    /// Gets or sets the ID of the user who created this auction.
    /// </summary>
    /// <value>The user ID of the auction master who initially set up the auction. 0 indicates system/admin created.</value>
    public int CreatedByUserId { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the date and time when the auction was created.
    /// </summary>
    /// <value>The UTC timestamp when the auction was first created.</value>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the auction was started.
    /// </summary>
    /// <value>The UTC timestamp when the auction status changed from Draft to InProgress, or null if not yet started.</value>
    public DateTime? StartedDate { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the auction was completed.
    /// </summary>
    /// <value>The UTC timestamp when the auction was finished, or null if not yet complete.</value>
    public DateTime? CompletedDate { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the user whose turn it is to nominate a school.
    /// </summary>
    /// <value>The user ID of the current nominator, or null if no active nomination is in progress.</value>
    public int? CurrentNominatorUserId { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the school currently being bid on.
    /// </summary>
    /// <value>The school ID of the currently nominated school, or null if no school is being bid on.</value>
    public int? CurrentSchoolId { get; set; }
    
    /// <summary>
    /// Gets or sets the current highest bid amount for the nominated school.
    /// </summary>
    /// <value>The highest bid amount in dollars, or null if no bids have been placed.</value>
    public decimal? CurrentHighBid { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the user who placed the current highest bid.
    /// </summary>
    /// <value>The user ID of the current high bidder, or null if no bids have been placed.</value>
    public int? CurrentHighBidderUserId { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the auction was last modified.
    /// </summary>
    /// <value>The UTC timestamp when any auction property was last updated.</value>
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    
    /// <summary>
    /// Gets or sets the user who created this auction.
    /// </summary>
    /// <value>The User entity representing the auction master, or null if not loaded.</value>
    public virtual User? CreatedByUser { get; set; }
    
    /// <summary>
    /// Gets or sets the user whose turn it is to nominate a school.
    /// </summary>
    /// <value>The User entity of the current nominator, or null if no nomination is in progress or not loaded.</value>
    public virtual User? CurrentNominatorUser { get; set; }
    
    /// <summary>
    /// Gets or sets the school currently being bid on.
    /// </summary>
    /// <value>The AuctionSchool entity currently nominated for bidding, or null if no school is being bid on or not loaded.</value>
    public virtual AuctionSchool? CurrentSchool { get; set; }
    
    /// <summary>
    /// Gets or sets the user who placed the current highest bid.
    /// </summary>
    /// <value>The User entity of the current high bidder, or null if no bids have been placed or not loaded.</value>
    public virtual User? CurrentHighBidderUser { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of users participating in this auction.
    /// </summary>
    /// <value>A collection of User entities representing all participants who have joined the auction.</value>
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    
    /// <summary>
    /// Gets or sets the collection of teams in this auction.
    /// </summary>
    /// <value>A collection of Team entities representing the fantasy teams competing in the auction.</value>
    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    
    /// <summary>
    /// Gets or sets the collection of roster positions defined for this auction.
    /// </summary>
    /// <value>A collection of RosterPosition entities defining the team composition and constraints.</value>
    public virtual ICollection<RosterPosition> RosterPositions { get; set; } = new List<RosterPosition>();
    
    /// <summary>
    /// Gets or sets the collection of schools available in this auction.
    /// </summary>
    /// <value>A collection of AuctionSchool entities representing schools that can be drafted with their auction-specific data.</value>
    public virtual ICollection<AuctionSchool> AuctionSchools { get; set; } = new List<AuctionSchool>();
    
    /// <summary>
    /// Gets or sets the collection of completed draft picks in this auction.
    /// </summary>
    /// <value>A collection of DraftPick entities representing schools that have been won by teams.</value>
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    
    /// <summary>
    /// Gets or sets the collection of all bid history for this auction.
    /// </summary>
    /// <value>A collection of BidHistory entities tracking all bids placed during the auction.</value>
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();
    
    /// <summary>
    /// Gets or sets the collection defining the nomination order for teams.
    /// </summary>
    /// <value>A collection of NominationOrder entities determining which team nominates next.</value>
    public virtual ICollection<NominationOrder> NominationOrders { get; set; } = new List<NominationOrder>();
}