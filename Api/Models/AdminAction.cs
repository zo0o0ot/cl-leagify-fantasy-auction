using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

/// <summary>
/// Represents an administrative action performed in the Leagify Fantasy Auction system.
/// Tracks high-level administrative operations for audit, compliance, and troubleshooting.
/// </summary>
/// <remarks>
/// AdminActions record operations performed by system administrators and auction masters
/// such as auction creation/deletion, user management, configuration changes, and manual interventions.
/// This provides accountability and enables analysis of system usage patterns.
/// </remarks>
public class AdminAction
{
    /// <summary>
    /// Gets or sets the unique identifier for this admin action.
    /// </summary>
    /// <value>The primary key used to identify this action record.</value>
    public int AdminActionId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the auction this action relates to.
    /// </summary>
    /// <value>The auction ID if this action is auction-specific, or null for system-level actions.</value>
    public int? AuctionId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who performed this action.
    /// </summary>
    /// <value>The user ID if performed by an auction master, or null for system administrator actions.</value>
    public int? AdminUserId { get; set; }

    /// <summary>
    /// Gets or sets the type of action performed.
    /// </summary>
    /// <value>
    /// The action category such as "AuctionCreated", "AuctionDeleted", "UserKicked", "RoleAssigned",
    /// "BiddingEnded", "SchoolReassigned", "AuctionArchived", "SystemMaintenance", etc.
    /// </value>
    [Required]
    [MaxLength(50)]
    public string ActionType { get; set; } = string.Empty; // Delete, Archive, ForceEnd, etc.

    /// <summary>
    /// Gets or sets a human-readable description of the action.
    /// </summary>
    /// <value>A detailed message explaining what occurred and why.</value>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entity type affected by this action.
    /// </summary>
    /// <value>The name of the entity type (e.g., "Auction", "User", "Team", "DraftPick").</value>
    [MaxLength(50)]
    public string? EntityType { get; set; }

    /// <summary>
    /// Gets or sets the ID of the specific entity affected.
    /// </summary>
    /// <value>The primary key of the entity that was modified, or null if not applicable.</value>
    public int? EntityId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the action in JSON format.
    /// </summary>
    /// <value>
    /// JSON-encoded additional context such as previous values, configuration changes,
    /// IP addresses, user agents, or other relevant diagnostic information.
    /// </value>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this action was performed.
    /// </summary>
    /// <value>The UTC timestamp when the action occurred.</value>
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the IP address from which the action was initiated.
    /// </summary>
    /// <value>The IPv4 or IPv6 address of the client, or null if not available.</value>
    [MaxLength(45)]
    public string? IPAddress { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the auction this action relates to.
    /// </summary>
    /// <value>The Auction entity, or null if not auction-specific or not loaded.</value>
    public virtual Auction? Auction { get; set; }

    /// <summary>
    /// Gets or sets the user who performed this action.
    /// </summary>
    /// <value>The User entity, or null if performed by system admin or not loaded.</value>
    public virtual User? AdminUser { get; set; }
}