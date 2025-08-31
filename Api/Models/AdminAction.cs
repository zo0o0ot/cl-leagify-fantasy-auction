using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class AdminAction
{
    public int AdminActionId { get; set; }
    
    public int? AuctionId { get; set; }
    
    public int? AdminUserId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty; // Delete, Archive, ForceEnd, etc.
    
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    
    [MaxLength(45)]
    public string? IPAddress { get; set; }

    // Navigation properties
    public virtual Auction? Auction { get; set; }
    public virtual User? AdminUser { get; set; }
}