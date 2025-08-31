using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class NominationOrder
{
    public int NominationOrderId { get; set; }
    
    public int AuctionId { get; set; }
    
    public int UserId { get; set; }
    
    public int OrderPosition { get; set; }
    
    public bool HasNominated { get; set; } = false;
    
    public bool IsSkipped { get; set; } = false;

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}