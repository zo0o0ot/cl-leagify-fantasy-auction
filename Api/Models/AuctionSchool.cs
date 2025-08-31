using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class AuctionSchool
{
    public int AuctionSchoolId { get; set; }
    
    public int AuctionId { get; set; }
    
    public int SchoolId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Conference { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string LeagifyPosition { get; set; } = string.Empty;
    
    public decimal ProjectedPoints { get; set; }
    
    public int NumberOfProspects { get; set; }
    
    public decimal? SuggestedAuctionValue { get; set; }
    
    public decimal ProjectedPointsAboveAverage { get; set; }
    
    public decimal ProjectedPointsAboveReplacement { get; set; }
    
    public decimal AveragePointsForPosition { get; set; }
    
    public decimal ReplacementValueAverageForPosition { get; set; }
    
    public bool IsAvailable { get; set; } = true;
    
    public int ImportOrder { get; set; }

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual School School { get; set; } = null!;
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();
}