using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

public class UserRole
{
    public int UserRoleId { get; set; }
    
    public int UserId { get; set; }
    
    public int? TeamId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty; // AuctionMaster, TeamCoach, ProxyCoach, Viewer

    /// <summary>
    /// Optional custom alias for Proxy Coach roles (e.g., "Cyber-Ross")
    /// </summary>
    [MaxLength(50)]
    public string? ProxyAlias { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Team? Team { get; set; }
}