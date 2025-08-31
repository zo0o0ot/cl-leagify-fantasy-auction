using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class Auction
{
    public int AuctionId { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string JoinCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string MasterRecoveryCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, InProgress, Complete, Archived
    
    public int CreatedByUserId { get; set; }
    
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? StartedDate { get; set; }
    
    public DateTime? CompletedDate { get; set; }
    
    public int? CurrentNominatorUserId { get; set; }
    
    public int? CurrentSchoolId { get; set; }
    
    public decimal? CurrentHighBid { get; set; }
    
    public int? CurrentHighBidderUserId { get; set; }
    
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User? CreatedByUser { get; set; }
    public virtual User? CurrentNominatorUser { get; set; }
    public virtual AuctionSchool? CurrentSchool { get; set; }
    public virtual User? CurrentHighBidderUser { get; set; }
    
    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    public virtual ICollection<RosterPosition> RosterPositions { get; set; } = new List<RosterPosition>();
    public virtual ICollection<AuctionSchool> AuctionSchools { get; set; } = new List<AuctionSchool>();
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();
    public virtual ICollection<NominationOrder> NominationOrders { get; set; } = new List<NominationOrder>();
}