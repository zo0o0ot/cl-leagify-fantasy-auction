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

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();
    public virtual ICollection<DraftPick> NominatedPicks { get; set; } = new List<DraftPick>();
    public virtual ICollection<DraftPick> WonPicks { get; set; } = new List<DraftPick>();
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();
    public virtual ICollection<NominationOrder> NominationOrders { get; set; } = new List<NominationOrder>();
}