using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class RosterPosition
{
    public int RosterPositionId { get; set; }
    
    public int AuctionId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string PositionName { get; set; } = string.Empty; // "Big Ten", "SEC", "Flex", etc.
    
    public int SlotsPerTeam { get; set; }
    
    [Required]
    [MaxLength(7)]
    public string ColorCode { get; set; } = string.Empty; // Hex color for UI (#FF5733)
    
    public int DisplayOrder { get; set; }
    
    public bool IsFlexPosition { get; set; } = false;

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
}