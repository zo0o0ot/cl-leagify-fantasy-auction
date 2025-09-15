using System.Text.Json;

namespace LeagifyFantasyAuction.Services;

/// <summary>
/// Mock service for testing auction functionality without database dependency.
/// Provides realistic mock data for join auction flow, role assignment, and auction state.
/// </summary>
public class MockAuctionService
{
    private readonly Dictionary<string, MockAuction> _auctions = new();
    private readonly Dictionary<string, MockUser> _users = new();
    private int _nextUserId = 1;
    private int _nextTeamId = 1;

    public MockAuctionService()
    {
        InitializeSampleAuctions();
    }

    /// <summary>
    /// Attempts to join an auction with the provided join code and display name.
    /// Returns session information if successful.
    /// </summary>
    public async Task<MockJoinResult> JoinAuctionAsync(string joinCode, string displayName)
    {
        await Task.Delay(500); // Simulate network delay

        joinCode = joinCode.Trim().ToUpperInvariant();
        displayName = displayName.Trim();

        // Find auction by join code
        var auction = _auctions.Values.FirstOrDefault(a =>
            a.JoinCode.Equals(joinCode, StringComparison.OrdinalIgnoreCase));

        if (auction == null)
        {
            return MockJoinResult.Error("Join code not found. Please check the code and try again.");
        }

        if (auction.Status != "Draft")
        {
            return MockJoinResult.Error("This auction is no longer accepting new participants.");
        }

        // Check for duplicate display name
        if (auction.Users.Any(u => u.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
        {
            return MockJoinResult.Error("This display name is already taken in this auction. Please choose a different name.");
        }

        // Create new user
        var user = new MockUser
        {
            UserId = _nextUserId++,
            AuctionId = auction.AuctionId,
            DisplayName = displayName,
            SessionToken = Guid.NewGuid().ToString(),
            IsConnected = true,
            JoinedDate = DateTime.UtcNow,
            LastActiveDate = DateTime.UtcNow,
            IsReconnectionPending = false
        };

        auction.Users.Add(user);
        _users[user.SessionToken] = user;

        return MockJoinResult.Success(new MockJoinResponse
        {
            UserId = user.UserId,
            AuctionId = auction.AuctionId,
            DisplayName = user.DisplayName,
            SessionToken = user.SessionToken,
            AuctionName = auction.Name
        });
    }

    /// <summary>
    /// Validates an existing session token and returns updated user information.
    /// </summary>
    public async Task<MockUser?> ValidateSessionAsync(string sessionToken)
    {
        await Task.Delay(200); // Simulate network delay

        if (_users.TryGetValue(sessionToken, out var user))
        {
            user.LastActiveDate = DateTime.UtcNow;
            return user;
        }

        return null;
    }

    /// <summary>
    /// Gets all participants for a specific auction with their current roles.
    /// </summary>
    public async Task<List<MockParticipant>> GetAuctionParticipantsAsync(int auctionId)
    {
        await Task.Delay(300); // Simulate network delay

        var auction = _auctions.Values.FirstOrDefault(a => a.AuctionId == auctionId);
        if (auction == null) return new List<MockParticipant>();

        return auction.Users.Select(u => new MockParticipant
        {
            UserId = u.UserId,
            DisplayName = u.DisplayName,
            IsConnected = u.IsConnected,
            JoinedDate = u.JoinedDate,
            LastActiveDate = u.LastActiveDate,
            IsReconnectionPending = u.IsReconnectionPending,
            Roles = u.Roles.ToList()
        }).ToList();
    }

    /// <summary>
    /// Assigns a role to a user (auction master functionality).
    /// </summary>
    public async Task<bool> AssignRoleAsync(int auctionId, int userId, string role, int? teamId = null)
    {
        await Task.Delay(200);

        var auction = _auctions.Values.FirstOrDefault(a => a.AuctionId == auctionId);
        var user = auction?.Users.FirstOrDefault(u => u.UserId == userId);

        if (user == null) return false;

        // Remove existing roles of the same type (users can only have one role)
        user.Roles.Clear();

        var newRole = new MockRole
        {
            UserRoleId = Random.Shared.Next(1000, 9999),
            Role = role,
            TeamId = teamId,
            TeamName = teamId.HasValue ? auction?.Teams.FirstOrDefault(t => t.TeamId == teamId)?.TeamName : null,
            AssignedDate = DateTime.UtcNow
        };

        user.Roles.Add(newRole);
        return true;
    }

    /// <summary>
    /// Handles user leaving an auction.
    /// </summary>
    public async Task<bool> LeaveAuctionAsync(string sessionToken)
    {
        await Task.Delay(300);

        if (!_users.TryGetValue(sessionToken, out var user)) return false;

        var auction = _auctions.Values.FirstOrDefault(a => a.AuctionId == user.AuctionId);
        if (auction == null) return false;

        // Remove user from auction
        auction.Users.RemoveAll(u => u.UserId == user.UserId);
        _users.Remove(sessionToken);

        return true;
    }

    /// <summary>
    /// Creates teams for testing role assignment.
    /// </summary>
    public async Task CreateTeamsAsync(int auctionId, int teamCount)
    {
        await Task.Delay(200);

        var auction = _auctions.Values.FirstOrDefault(a => a.AuctionId == auctionId);
        if (auction == null) return;

        auction.Teams.Clear();

        for (int i = 1; i <= teamCount; i++)
        {
            auction.Teams.Add(new MockTeam
            {
                TeamId = _nextTeamId++,
                AuctionId = auctionId,
                TeamName = $"Team {i}",
                Budget = 1000,
                RemainingBudget = 1000,
                NominationOrder = i,
                IsActive = true
            });
        }
    }

    private void InitializeSampleAuctions()
    {
        // Create sample auction for testing
        var auction = new MockAuction
        {
            AuctionId = 1,
            Name = "Mock NFL Draft League 2024",
            JoinCode = "MOCK24",
            MasterRecoveryCode = "MASTER123",
            Status = "Draft",
            CreatedDate = DateTime.UtcNow.AddHours(-2),
            Users = new List<MockUser>(),
            Teams = new List<MockTeam>()
        };

        _auctions[auction.JoinCode] = auction;

        // Add some sample teams
        auction.Teams.AddRange(new[]
        {
            new MockTeam { TeamId = _nextTeamId++, AuctionId = 1, TeamName = "Team Alpha", Budget = 1000, RemainingBudget = 1000, NominationOrder = 1, IsActive = true },
            new MockTeam { TeamId = _nextTeamId++, AuctionId = 1, TeamName = "Team Beta", Budget = 1000, RemainingBudget = 1000, NominationOrder = 2, IsActive = true },
            new MockTeam { TeamId = _nextTeamId++, AuctionId = 1, TeamName = "Team Gamma", Budget = 1000, RemainingBudget = 1000, NominationOrder = 3, IsActive = true }
        });

        // Create second auction for testing multiple auctions
        var auction2 = new MockAuction
        {
            AuctionId = 2,
            Name = "Test Fantasy Baseball",
            JoinCode = "BASE24",
            MasterRecoveryCode = "MASTER456",
            Status = "Draft",
            CreatedDate = DateTime.UtcNow.AddHours(-1),
            Users = new List<MockUser>(),
            Teams = new List<MockTeam>()
        };

        _auctions[auction2.JoinCode] = auction2;
    }
}

// Mock data models
public class MockAuction
{
    public int AuctionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public string MasterRecoveryCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedDate { get; set; }
    public List<MockUser> Users { get; set; } = new();
    public List<MockTeam> Teams { get; set; } = new();
}

public class MockUser
{
    public int UserId { get; set; }
    public int AuctionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime JoinedDate { get; set; }
    public DateTime LastActiveDate { get; set; }
    public bool IsReconnectionPending { get; set; }
    public List<MockRole> Roles { get; set; } = new();
}

public class MockTeam
{
    public int TeamId { get; set; }
    public int AuctionId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal RemainingBudget { get; set; }
    public int NominationOrder { get; set; }
    public bool IsActive { get; set; }
}

public class MockRole
{
    public int UserRoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime AssignedDate { get; set; }
}

public class MockParticipant
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime JoinedDate { get; set; }
    public DateTime LastActiveDate { get; set; }
    public bool IsReconnectionPending { get; set; }
    public List<MockRole> Roles { get; set; } = new();
}

public class MockJoinResponse
{
    public int UserId { get; set; }
    public int AuctionId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string AuctionName { get; set; } = string.Empty;
}

public class MockJoinResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public MockJoinResponse? Response { get; set; }

    public static MockJoinResult Success(MockJoinResponse response) => new()
    {
        IsSuccess = true,
        Response = response
    };

    public static MockJoinResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}