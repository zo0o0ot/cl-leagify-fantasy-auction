using LeagifyFantasyAuction.Api.Models;

namespace LeagifyFantasyAuction.Api.Services;

/// <summary>
/// Defines the contract for auction management services in the Leagify Fantasy Auction system.
/// Provides operations for creating, managing, and validating auctions including join code handling.
/// </summary>
/// <remarks>
/// This service handles the core auction lifecycle including creation, join code generation and validation,
/// status management, and basic CRUD operations. It ensures join codes are unique and provides
/// secure auction access patterns.
/// </remarks>
public interface IAuctionService
{
    /// <summary>
    /// Creates a new auction with automatically generated join codes.
    /// </summary>
    /// <param name="name">The display name for the auction.</param>
    /// <param name="createdByUserId">The ID of the user creating the auction (auction master).</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created auction with generated codes.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when unable to generate unique codes after maximum attempts.</exception>
    /// <remarks>
    /// Automatically generates a unique 6-character join code and a unique 16-character master recovery code.
    /// The auction is created in "Draft" status and can be configured before being started.
    /// </remarks>
    Task<Auction> CreateAuctionAsync(string name, int createdByUserId);

    /// <summary>
    /// Retrieves an auction by its unique join code.
    /// </summary>
    /// <param name="joinCode">The join code to search for (case-insensitive).</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the auction if found, or null if not found.</returns>
    /// <remarks>
    /// Join code comparison is case-insensitive. Returns null if no auction is found with the specified code.
    /// </remarks>
    Task<Auction?> GetAuctionByJoinCodeAsync(string joinCode);

    /// <summary>
    /// Retrieves an auction by its unique master recovery code.
    /// </summary>
    /// <param name="masterRecoveryCode">The master recovery code to search for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the auction if found, or null if not found.</returns>
    /// <remarks>
    /// Master recovery codes are case-sensitive and should only be used by auction masters.
    /// Returns null if no auction is found with the specified code.
    /// </remarks>
    Task<Auction?> GetAuctionByMasterRecoveryCodeAsync(string masterRecoveryCode);

    /// <summary>
    /// Retrieves an auction by its unique identifier.
    /// </summary>
    /// <param name="auctionId">The auction ID to search for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the auction if found, or null if not found.</returns>
    Task<Auction?> GetAuctionByIdAsync(int auctionId);

    /// <summary>
    /// Retrieves all auctions in the system.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of all auctions.</returns>
    /// <remarks>
    /// Returns all auctions regardless of status. For management interface use only.
    /// Results are not filtered or ordered by default.
    /// </remarks>
    Task<List<Auction>> GetAllAuctionsAsync();

    /// <summary>
    /// Updates the status of an auction.
    /// </summary>
    /// <param name="auctionId">The ID of the auction to update.</param>
    /// <param name="newStatus">The new status value. Must be one of: "Draft", "InProgress", "Complete", "Archived".</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the update was successful.</returns>
    /// <exception cref="ArgumentException">Thrown when newStatus is not a valid status value.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the status transition is not allowed.</exception>
    /// <remarks>
    /// Validates that the status transition is allowed and updates the corresponding date fields:
    /// - Draft to InProgress: Sets StartedDate
    /// - InProgress to Complete: Sets CompletedDate
    /// Invalid transitions (e.g., Complete to Draft) will throw an exception.
    /// </remarks>
    Task<bool> UpdateAuctionStatusAsync(int auctionId, string newStatus);

    /// <summary>
    /// Validates that a join code meets the format requirements and is unique.
    /// </summary>
    /// <param name="joinCode">The join code to validate.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains validation results.</returns>
    /// <remarks>
    /// Checks that the join code is the correct length, contains only alphanumeric characters,
    /// and is not already in use by another auction.
    /// </remarks>
    Task<(bool IsValid, string? ErrorMessage)> ValidateJoinCodeAsync(string joinCode);

    /// <summary>
    /// Generates a unique join code that is not currently in use.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a unique 6-character alphanumeric join code.</returns>
    /// <exception cref="InvalidOperationException">Thrown when unable to generate a unique code after maximum attempts.</exception>
    /// <remarks>
    /// Generates random 6-character codes using uppercase letters and numbers, excluding potentially
    /// confusing characters like 0, O, 1, I. Attempts up to 100 times to find a unique code.
    /// </remarks>
    Task<string> GenerateUniqueJoinCodeAsync();

    /// <summary>
    /// Generates a unique master recovery code that is not currently in use.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains a unique 16-character alphanumeric master recovery code.</returns>
    /// <exception cref="InvalidOperationException">Thrown when unable to generate a unique code after maximum attempts.</exception>
    /// <remarks>
    /// Generates random 16-character codes with a mix of uppercase, lowercase, and numbers for enhanced security.
    /// Attempts up to 100 times to find a unique code.
    /// </remarks>
    Task<string> GenerateUniqueMasterRecoveryCodeAsync();
}