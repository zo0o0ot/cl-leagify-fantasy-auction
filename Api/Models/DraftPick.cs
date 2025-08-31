using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class DraftPick
{
    public int DraftPickId { get; set; }
    
    public int AuctionId { get; set; }
    
    public int TeamId { get; set; }
    
    public int AuctionSchoolId { get; set; }
    
    public int RosterPositionId { get; set; }
    
    public decimal WinningBid { get; set; }
    
    public int NominatedByUserId { get; set; }
    
    public int WonByUserId { get; set; }
    
    public int PickOrder { get; set; }
    
    public DateTime DraftedDate { get; set; } = DateTime.UtcNow;
    
    public bool IsAssignmentConfirmed { get; set; } = false;

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual Team Team { get; set; } = null!;
    public virtual AuctionSchool AuctionSchool { get; set; } = null!;
    public virtual RosterPosition RosterPosition { get; set; } = null!;
    public virtual User NominatedByUser { get; set; } = null!;
    public virtual User WonByUser { get; set; } = null!;
}