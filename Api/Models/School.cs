using System.ComponentModel.DataAnnotations;

namespace LeagifyFantasyAuction.Api.Models;

/// <summary>
/// Represents a school entity in the Leagify Fantasy Auction system.
/// Schools are persistent entities that can be referenced across multiple auctions.
/// </summary>
/// <remarks>
/// This is the main Entity Framework model for schools, with proper data annotations and navigation properties.
/// Schools must have unique names and can have optional logo information.
/// The navigation properties enable relationships with auction-specific school data.
/// </remarks>
public class School
{
    /// <summary>
    /// Gets or sets the unique identifier for the school.
    /// </summary>
    /// <value>The primary key used to identify this school across the system.</value>
    public int SchoolId { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the school.
    /// </summary>
    /// <value>The full name of the school. Must be unique and cannot exceed 100 characters.</value>
    /// <remarks>
    /// This field is required and has a maximum length of 100 characters.
    /// School names should be unique across the system to avoid confusion.
    /// </remarks>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the URL to the school's logo image.
    /// </summary>
    /// <value>A valid URL pointing to the school's logo image, or null if no logo is available.</value>
    /// <remarks>
    /// This field can store URLs up to 500 characters in length.
    /// The URL should point to a publicly accessible image file.
    /// </remarks>
    [MaxLength(500)]
    public string? LogoURL { get; set; }
    
    /// <summary>
    /// Gets or sets the filename of the locally stored logo image.
    /// </summary>
    /// <value>The filename of the logo image after it has been downloaded and stored locally, or null if no logo is stored.</value>
    /// <remarks>
    /// This field stores the local filename (up to 100 characters) of downloaded logo images.
    /// Used in conjunction with LogoURL to manage both remote and local logo references.
    /// </remarks>
    [MaxLength(100)]
    public string? LogoFileName { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the school was created.
    /// </summary>
    /// <value>The UTC timestamp when the school was first added to the system.</value>
    /// <remarks>
    /// Defaults to the current UTC time when a new school is created.
    /// This field is automatically managed by the database through default value constraints.
    /// </remarks>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the school was last modified.
    /// </summary>
    /// <value>The UTC timestamp when the school was last updated.</value>
    /// <remarks>
    /// Should be updated whenever any school property is modified.
    /// Defaults to the current UTC time and is managed by database triggers or application logic.
    /// </remarks>
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of auction-specific school data associated with this school.
    /// </summary>
    /// <value>A collection of AuctionSchool entities that reference this school in specific auctions.</value>
    /// <remarks>
    /// This navigation property enables Entity Framework to handle the one-to-many relationship
    /// between schools and their auction-specific data (statistics, availability, etc.).
    /// A single school can participate in multiple auctions with different characteristics.
    /// </remarks>
    public virtual ICollection<AuctionSchool> AuctionSchools { get; set; } = new List<AuctionSchool>();
}