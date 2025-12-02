using Microsoft.EntityFrameworkCore;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Models;
using Microsoft.Extensions.Logging;

namespace LeagifyFantasyAuction.Api.Services;

/// <summary>
/// Provides auction management services for the Leagify Fantasy Auction system.
/// Handles auction creation, join code generation, validation, and basic CRUD operations.
/// </summary>
/// <remarks>
/// This service uses Entity Framework Core for database operations and implements thread-safe
/// join code generation with collision detection. All database operations are performed
/// asynchronously for optimal performance.
/// </remarks>
public class AuctionService : IAuctionService
{
    private readonly LeagifyAuctionDbContext _context;
    private readonly ILogger<AuctionService> _logger;
    private readonly Random _random = new();
    
    // Valid characters for join codes (excluding confusing characters like 0, O, 1, I)
    private const string JoinCodeChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    
    // Valid characters for master recovery codes (more complex for security)
    private const string MasterCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
    
    private static readonly HashSet<string> ValidStatuses = new()
    {
        "Draft", "InProgress", "Paused", "Complete", "Archived"
    };

    /// <summary>
    /// Initializes a new instance of the AuctionService class.
    /// </summary>
    /// <param name="context">The database context for auction operations.</param>
    /// <param name="logger">The logger for service operations.</param>
    public AuctionService(LeagifyAuctionDbContext context, ILogger<AuctionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Auction> CreateAuctionAsync(string name, int? createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Auction name cannot be null or empty.", nameof(name));
        }

        _logger.LogInformation("Creating new auction '{AuctionName}' for user {UserId}", name, createdByUserId);

        var joinCode = await GenerateUniqueJoinCodeAsync();
        _logger.LogInformation("Generated join code: {JoinCode}", joinCode);
        
        var masterRecoveryCode = await GenerateUniqueMasterRecoveryCodeAsync();
        _logger.LogInformation("Generated master recovery code: {MasterCode}", masterRecoveryCode);

        var auction = new Auction
        {
            Name = name.Trim(),
            JoinCode = joinCode,
            MasterRecoveryCode = masterRecoveryCode,
            Status = "Draft",
            CreatedByUserId = createdByUserId,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        _logger.LogInformation("Adding auction to context: Name={Name}, JoinCode={JoinCode}, MasterCode={MasterCode}, CreatedByUserId={CreatedByUserId}", 
            auction.Name, auction.JoinCode, auction.MasterRecoveryCode, auction.CreatedByUserId);
        
        _context.Auctions.Add(auction);
        
        _logger.LogInformation("Calling SaveChangesAsync...");
        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("SaveChanges completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveChanges failed - Name: {Name}, JoinCode: {JoinCode}, CreatedByUserId: {CreatedByUserId}", 
                auction.Name, auction.JoinCode, auction.CreatedByUserId);
            
            // Log inner exception details to help diagnose the specific constraint violation
            var innerEx = ex.InnerException;
            while (innerEx != null)
            {
                _logger.LogError("Inner exception: {InnerExceptionType} - {InnerExceptionMessage}", 
                    innerEx.GetType().Name, innerEx.Message);
                innerEx = innerEx.InnerException;
            }
            throw;
        }

        _logger.LogInformation("Created auction {AuctionId} with join code {JoinCode}", 
            auction.AuctionId, auction.JoinCode);

        return auction;
    }

    /// <inheritdoc />
    public async Task<Auction?> GetAuctionByJoinCodeAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return null;
        }

        return await _context.Auctions
            .FirstOrDefaultAsync(a => a.JoinCode.ToLower() == joinCode.ToLower());
    }

    /// <inheritdoc />
    public async Task<Auction?> GetAuctionByMasterRecoveryCodeAsync(string masterRecoveryCode)
    {
        if (string.IsNullOrWhiteSpace(masterRecoveryCode))
        {
            return null;
        }

        return await _context.Auctions
            .FirstOrDefaultAsync(a => a.MasterRecoveryCode == masterRecoveryCode);
    }

    /// <inheritdoc />
    public async Task<Auction?> GetAuctionByIdAsync(int auctionId)
    {
        return await _context.Auctions
            .FirstOrDefaultAsync(a => a.AuctionId == auctionId);
    }

    /// <inheritdoc />
    public async Task<List<Auction>> GetAllAuctionsAsync()
    {
        return await _context.Auctions.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAuctionStatusAsync(int auctionId, string newStatus)
    {
        if (!ValidStatuses.Contains(newStatus))
        {
            throw new ArgumentException($"Invalid status '{newStatus}'. Valid statuses are: {string.Join(", ", ValidStatuses)}", 
                nameof(newStatus));
        }

        var auction = await _context.Auctions.FindAsync(auctionId);
        if (auction == null)
        {
            _logger.LogWarning("Attempted to update status of non-existent auction {AuctionId}", auctionId);
            return false;
        }

        var oldStatus = auction.Status;
        
        // Validate status transitions
        if (!IsValidStatusTransition(oldStatus, newStatus))
        {
            throw new InvalidOperationException($"Invalid status transition from '{oldStatus}' to '{newStatus}'");
        }

        auction.Status = newStatus;
        auction.ModifiedDate = DateTime.UtcNow;

        // Set appropriate date fields based on status
        switch (newStatus)
        {
            case "InProgress":
                if (auction.StartedDate == null)
                {
                    auction.StartedDate = DateTime.UtcNow;
                }
                break;
            case "Complete":
                if (auction.CompletedDate == null)
                {
                    auction.CompletedDate = DateTime.UtcNow;
                }
                break;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated auction {AuctionId} status from '{OldStatus}' to '{NewStatus}'", 
            auctionId, oldStatus, newStatus);

        return true;
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateJoinCodeAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return (false, "Join code cannot be empty");
        }

        // Check length
        if (joinCode.Length != 6)
        {
            return (false, "Join code must be exactly 6 characters");
        }

        // Check characters
        if (!joinCode.All(c => JoinCodeChars.Contains(char.ToUpper(c))))
        {
            return (false, "Join code contains invalid characters");
        }

        // Check uniqueness
        var exists = await _context.Auctions
            .AnyAsync(a => a.JoinCode.ToLower() == joinCode.ToLower());

        if (exists)
        {
            return (false, "Join code is already in use");
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<string> GenerateUniqueJoinCodeAsync()
    {
        const int maxAttempts = 100;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateJoinCode();
            var (isValid, _) = await ValidateJoinCodeAsync(code);
            
            if (isValid)
            {
                _logger.LogDebug("Generated unique join code after {Attempts} attempts", attempt + 1);
                return code;
            }
        }

        throw new InvalidOperationException($"Unable to generate unique join code after {maxAttempts} attempts");
    }

    /// <inheritdoc />
    public async Task<string> GenerateUniqueMasterRecoveryCodeAsync()
    {
        const int maxAttempts = 100;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateMasterRecoveryCode();
            
            var exists = await _context.Auctions
                .AnyAsync(a => a.MasterRecoveryCode == code);
            
            if (!exists)
            {
                _logger.LogDebug("Generated unique master recovery code after {Attempts} attempts", attempt + 1);
                return code;
            }
        }

        throw new InvalidOperationException($"Unable to generate unique master recovery code after {maxAttempts} attempts");
    }

    /// <summary>
    /// Generates a 6-character join code using safe characters.
    /// </summary>
    /// <returns>A 6-character alphanumeric join code.</returns>
    private string GenerateJoinCode()
    {
        return new string(Enumerable.Repeat(JoinCodeChars, 6)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Generates a 16-character master recovery code using a larger character set.
    /// </summary>
    /// <returns>A 16-character alphanumeric master recovery code.</returns>
    private string GenerateMasterRecoveryCode()
    {
        return new string(Enumerable.Repeat(MasterCodeChars, 16)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Validates whether a status transition is allowed.
    /// </summary>
    /// <param name="fromStatus">The current status.</param>
    /// <param name="toStatus">The target status.</param>
    /// <returns>True if the transition is allowed, false otherwise.</returns>
    private static bool IsValidStatusTransition(string fromStatus, string toStatus)
    {
        // Allow same status (no-op)
        if (fromStatus == toStatus)
        {
            return true;
        }

        return fromStatus switch
        {
            "Draft" => toStatus == "InProgress" || toStatus == "Archived",
            "InProgress" => toStatus == "Paused" || toStatus == "Complete" || toStatus == "Archived",
            "Paused" => toStatus == "InProgress" || toStatus == "Complete" || toStatus == "Archived",
            "Complete" => toStatus == "Archived",
            "Archived" => false, // No transitions out of archived
            _ => false
        };
    }
}