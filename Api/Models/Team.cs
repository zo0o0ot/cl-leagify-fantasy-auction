using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class Team
{
    public int TeamId { get; set; }
    
    public int AuctionId { get; set; }
    
    public int UserId { get; set; }
    
    [MaxLength(50)]
    public string? TeamName { get; set; }
    
    public decimal Budget { get; set; }
    
    public decimal RemainingBudget { get; set; }
    
    public int NominationOrder { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
}