using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

/// <summary>
/// Represents auction-specific data for a school participating in a fantasy auction.
/// Links schools to auctions with statistical data, draft status, and auction-specific attributes.
/// </summary>
/// <remarks>
/// This entity bridges the relationship between persistent schools and specific auctions.
/// Contains projected statistics, draft values, and availability status for auction gameplay.
/// Each school can have different statistics and values across different auctions.
/// </remarks>
public class AuctionSchool
{
    /// <summary>
    /// Gets or sets the unique identifier for this auction-school relationship.
    /// </summary>
    /// <value>The primary key for this specific school's participation in an auction.</value>
    public int AuctionSchoolId { get; set; }
    
    /// <summary>
    /// Gets or sets the identifier of the auction this school is participating in.
    /// </summary>
    /// <value>Foreign key reference to the Auction entity.</value>
    public int AuctionId { get; set; }
    
    /// <summary>
    /// Gets or sets the identifier of the school participating in this auction.
    /// </summary>
    /// <value>Foreign key reference to the School entity.</value>
    public int SchoolId { get; set; }
    
    /// <summary>
    /// Gets or sets the athletic conference the school belongs to.
    /// </summary>
    /// <value>The name of the school's athletic conference (e.g., "SEC", "Big Ten", "ACC").</value>
    /// <remarks>
    /// This field is required and limited to 50 characters.
    /// Used for grouping and filtering schools in auction displays.
    /// </remarks>
    [Required]
    [MaxLength(50)]
    public string Conference { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the fantasy position category for this school in the auction.
    /// </summary>
    /// <value>The Leagify-specific position designation (e.g., "Power Conference", "Mid-Major", "FCS").</value>
    /// <remarks>
    /// This field is required and limited to 50 characters.
    /// Determines which roster slots this school can fill and affects draft strategy.
    /// </remarks>
    [Required]
    [MaxLength(50)]
    public string LeagifyPosition { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the projected fantasy points for this school in the auction.
    /// </summary>
    /// <value>The expected fantasy points this school will generate based on statistical projections.</value>
    /// <remarks>
    /// Used as the primary metric for school valuation and draft strategy.
    /// Precision is maintained using decimal type for accurate calculations.
    /// </remarks>
    public decimal ProjectedPoints { get; set; }
    
    /// <summary>
    /// Gets or sets the number of NFL draft prospects expected from this school.
    /// </summary>
    /// <value>The count of players from this school projected to be drafted into the NFL.</value>
    /// <remarks>
    /// This metric contributes to the overall fantasy value calculation.
    /// Higher prospect counts typically correlate with higher fantasy point projections.
    /// </remarks>
    public int NumberOfProspects { get; set; }
    
    /// <summary>
    /// Gets or sets the suggested auction value for bidding on this school.
    /// </summary>
    /// <value>The recommended auction price based on projected performance, or null if not calculated.</value>
    /// <remarks>
    /// This value helps guide bidding strategy but is not enforced by the auction system.
    /// Calculated based on projected points, position scarcity, and league settings.
    /// </remarks>
    public decimal? SuggestedAuctionValue { get; set; }
    
    /// <summary>
    /// Gets or sets how many projected points this school is expected to score above the position average.
    /// </summary>
    /// <value>The difference between this school's projected points and the average for their position.</value>
    /// <remarks>
    /// Positive values indicate above-average performance; negative values indicate below-average.
    /// Used in value-based drafting calculations and auction strategy.
    /// </remarks>
    public decimal ProjectedPointsAboveAverage { get; set; }
    
    /// <summary>
    /// Gets or sets how many projected points this school is expected to score above replacement level.
    /// </summary>
    /// <value>The difference between this school's projected points and the replacement-level baseline for their position.</value>
    /// <remarks>
    /// This is a key metric in fantasy sports for determining true player value.
    /// Replacement level is typically defined as the last startable player at each position.
    /// </remarks>
    public decimal ProjectedPointsAboveReplacement { get; set; }
    
    /// <summary>
    /// Gets or sets the average projected points for all schools at this position.
    /// </summary>
    /// <value>The calculated average fantasy points for schools in the same LeagifyPosition.</value>
    /// <remarks>
    /// Used as a baseline for calculating ProjectedPointsAboveAverage.
    /// This value is consistent across all schools in the same position within an auction.
    /// </remarks>
    public decimal AveragePointsForPosition { get; set; }
    
    /// <summary>
    /// Gets or sets the replacement-level fantasy points threshold for this position.
    /// </summary>
    /// <value>The baseline fantasy points representing replacement-level performance for this position.</value>
    /// <remarks>
    /// Used as the baseline for calculating ProjectedPointsAboveReplacement.
    /// Represents the fantasy points of the worst startable school at this position.
    /// </remarks>
    public decimal ReplacementValueAverageForPosition { get; set; }
    
    /// <summary>
    /// Gets or sets whether this school is available for drafting in the auction.
    /// </summary>
    /// <value>True if the school can be nominated and bid on; false if already drafted or unavailable.</value>
    /// <remarks>
    /// Defaults to true when schools are imported.
    /// Set to false when a school is successfully drafted by a team.
    /// Used to filter available schools in auction interfaces.
    /// </remarks>
    public bool IsAvailable { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the order in which this school was imported from the CSV data.
    /// </summary>
    /// <value>The sequential position of this school in the original import file.</value>
    /// <remarks>
    /// Preserves the original ordering from CSV imports for administrative reference.
    /// Can be used to maintain consistent school presentation order across the application.
    /// </remarks>
    public int ImportOrder { get; set; }

    /// <summary>
    /// Gets or sets the auction this school is participating in.
    /// </summary>
    /// <value>Navigation property to the related Auction entity.</value>
    /// <remarks>
    /// Required navigation property for Entity Framework relationship mapping.
    /// Provides access to auction details such as settings, participants, and status.
    /// </remarks>
    public virtual Auction Auction { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the school entity that this auction school references.
    /// </summary>
    /// <value>Navigation property to the related School entity.</value>
    /// <remarks>
    /// Required navigation property providing access to persistent school information.
    /// Contains school name, logo, and other data that persists across multiple auctions.
    /// </remarks>
    public virtual School School { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the collection of draft picks associated with this auction school.
    /// </summary>
    /// <value>Collection of DraftPick entities where this school was selected.</value>
    /// <remarks>
    /// Typically contains zero or one draft pick, as schools can only be drafted once per auction.
    /// Used to track which team drafted the school and at what price.
    /// </remarks>
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    
    /// <summary>
    /// Gets or sets the collection of bid history records for this school in the auction.
    /// </summary>
    /// <value>Collection of BidHistory entities tracking all bids placed on this school.</value>
    /// <remarks>
    /// Contains the complete bidding history including bid amounts, timestamps, and bidders.
    /// Used for auction transparency and post-auction analysis.
    /// </remarks>
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();
}