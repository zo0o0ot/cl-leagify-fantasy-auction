using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

/// <summary>
/// Represents a roster position configuration within an auction's team structure.
/// Defines how many slots each team has for specific position types and their visual representation.
/// </summary>
/// <remarks>
/// Roster positions determine the structure of fantasy teams, specifying constraints like "2 Power Conference" or "1 Flex".
/// Each position has a display color for UI organization and can be marked as a flexible position that accepts multiple school types.
/// The auction master configures these positions during auction setup to define team composition rules.
/// </remarks>
public class RosterPosition
{
    /// <summary>
    /// Gets or sets the unique identifier for this roster position.
    /// </summary>
    /// <value>The primary key used to identify this position configuration across the system.</value>
    public int RosterPositionId { get; set; }
    
    /// <summary>
    /// Gets or sets the identifier of the auction this position configuration belongs to.
    /// </summary>
    /// <value>Foreign key reference to the Auction entity.</value>
    public int AuctionId { get; set; }
    
    /// <summary>
    /// Gets or sets the display name for this roster position.
    /// </summary>
    /// <value>The human-readable name shown to users (e.g., "Big Ten", "SEC", "Power Conference", "Flex").</value>
    /// <remarks>
    /// This name appears in roster displays, draft interfaces, and team management screens.
    /// Should be clear and descriptive to help users understand position requirements.
    /// </remarks>
    [Required]
    [MaxLength(50)]
    public string PositionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets how many roster slots of this position type each team has.
    /// </summary>
    /// <value>The number of schools each team must draft for this position type.</value>
    /// <remarks>
    /// Determines team composition requirements. For example, a value of 2 for "Power Conference" 
    /// means each team needs exactly 2 Power Conference schools on their roster.
    /// Used in budget validation and roster completion calculations.
    /// </remarks>
    public int SlotsPerTeam { get; set; }
    
    /// <summary>
    /// Gets or sets the hex color code used for visual representation of this position.
    /// </summary>
    /// <value>A 7-character hex color code including the # symbol (e.g., "#FF5733").</value>
    /// <remarks>
    /// Used throughout the UI to color-code roster positions for easy identification.
    /// Helps users quickly distinguish between different position types in complex roster displays.
    /// Must be a valid hex color format for proper rendering.
    /// </remarks>
    [Required]
    [MaxLength(7)]
    public string ColorCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the display order for this position in roster and draft interfaces.
    /// </summary>
    /// <value>The sort priority for positioning this slot type in UI displays.</value>
    /// <remarks>
    /// Lower numbers appear first in roster displays and draft interfaces.
    /// Allows auction masters to organize positions logically (e.g., power conferences first, then mid-majors, then flex slots).
    /// </remarks>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// Gets or sets whether this position can accept schools from multiple position categories.
    /// </summary>
    /// <value>True if this position accepts any school type; false if restricted to specific categories.</value>
    /// <remarks>
    /// Flex positions provide strategic flexibility by allowing teams to fill slots with any available school.
    /// Non-flex positions enforce strict category requirements (e.g., "SEC" positions only accept SEC schools).
    /// Affects roster validation and bid eligibility calculations.
    /// </remarks>
    public bool IsFlexPosition { get; set; } = false;

    /// <summary>
    /// Gets or sets the auction this position configuration belongs to.
    /// </summary>
    /// <value>Navigation property to the related Auction entity.</value>
    /// <remarks>
    /// Required navigation property for Entity Framework relationship mapping.
    /// Provides access to auction details such as settings, participants, and status.
    /// </remarks>
    public virtual Auction Auction { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the collection of draft picks assigned to this roster position.
    /// </summary>
    /// <value>Collection of DraftPick entities where schools were assigned to this position slot.</value>
    /// <remarks>
    /// Tracks which schools have been drafted and assigned to specific roster position slots.
    /// Used for roster validation, team composition analysis, and auction completion tracking.
    /// Each draft pick represents one filled slot of this position type.
    /// </remarks>
    public virtual ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
}