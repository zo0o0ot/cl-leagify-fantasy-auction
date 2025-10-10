using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class BidHistory
{
    public int BidHistoryId { get; set; }
    
    public int AuctionId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the auction school being bid on.
    /// Nullable to support test bids in waiting room (virtual Vermont A&M school).
    /// </summary>
    public int? AuctionSchoolId { get; set; }

    public int UserId { get; set; }
    
    public decimal BidAmount { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string BidType { get; set; } = string.Empty; // Nomination, Bid, Pass
    
    public DateTime BidDate { get; set; } = DateTime.UtcNow;
    
    public bool IsWinningBid { get; set; } = false;
    
    [MaxLength(200)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual AuctionSchool? AuctionSchool { get; set; }
    public virtual User User { get; set; } = null!;
}