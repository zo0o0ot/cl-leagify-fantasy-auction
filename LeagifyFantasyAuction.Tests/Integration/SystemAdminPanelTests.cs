using System.Text;
using System.Text.Json;
using FluentAssertions;
using System.Net;

namespace LeagifyFantasyAuction.Tests.Integration;

/// <summary>
/// Tests for the System Administration panel functionality
/// Verifies that site administrators can access and use diagnostic tools
/// </summary>
public class SystemAdminPanelTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api";
    private const string ManagementToken = "leagify-admin-2024";

    public SystemAdminPanelTests()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Management-Token", ManagementToken);
    }

    [Fact]
    public async Task GetAuctionSummary_ShouldReturnAllAuctionsWithMetadata()
    {
        // Arrange & Act
        var response = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auctions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        var data = JsonSerializer.Deserialize<JsonElement>(content);
        data.TryGetProperty("TotalAuctions", out var totalAuctions).Should().BeTrue();
        data.TryGetProperty("Auctions", out var auctions).Should().BeTrue();

        // Should include auction metadata
        if (auctions.GetArrayLength() > 0)
        {
            var firstAuction = auctions[0];
            firstAuction.TryGetProperty("AuctionId", out _).Should().BeTrue();
            firstAuction.TryGetProperty("Name", out _).Should().BeTrue();
            firstAuction.TryGetProperty("JoinCode", out _).Should().BeTrue();
            firstAuction.TryGetProperty("Status", out _).Should().BeTrue();
            firstAuction.TryGetProperty("ParticipantCount", out _).Should().BeTrue();
            firstAuction.TryGetProperty("TeamCount", out _).Should().BeTrue();
            firstAuction.TryGetProperty("CreatedDate", out _).Should().BeTrue();
        }

        Console.WriteLine($"✅ Retrieved {totalAuctions.GetInt32()} auctions successfully");
    }

    [Fact]
    public async Task CreateTestData_ShouldCreateDebugAuctionWithParticipants()
    {
        // Arrange & Act
        var response = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/create-test-data", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(content);

        data.TryGetProperty("success", out var success).Should().BeTrue();
        success.GetBoolean().Should().BeTrue();

        data.TryGetProperty("auctionId", out var auctionId).Should().BeTrue();
        auctionId.GetInt32().Should().BeGreaterThan(0);

        Console.WriteLine($"✅ Created test auction with ID: {auctionId.GetInt32()}");

        // Verify the created auction has participants
        var participantsResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId.GetInt32()}/participants");
        participantsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var participantsContent = await participantsResponse.Content.ReadAsStringAsync();
        var participantsData = JsonSerializer.Deserialize<JsonElement>(participantsContent);

        participantsData.TryGetProperty("participants", out var participants).Should().BeTrue();
        participants.GetArrayLength().Should().BeGreaterThan(0);

        Console.WriteLine($"✅ Test auction has {participants.GetArrayLength()} participants");
    }

    [Fact]
    public async Task GetDetailedParticipants_ShouldReturnParticipantRoleData()
    {
        // Arrange - Use a known auction ID (37 from previous testing)
        int testAuctionId = 37;

        // Act
        var response = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{testAuctionId}/participants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(content);

        data.TryGetProperty("auctionId", out var auctionId).Should().BeTrue();
        auctionId.GetInt32().Should().Be(testAuctionId);

        data.TryGetProperty("participants", out var participants).Should().BeTrue();

        // Verify participant structure includes team assignment data
        if (participants.GetArrayLength() > 0)
        {
            var firstParticipant = participants[0];
            firstParticipant.TryGetProperty("UserId", out _).Should().BeTrue();
            firstParticipant.TryGetProperty("DisplayName", out _).Should().BeTrue();
            firstParticipant.TryGetProperty("IsConnected", out _).Should().BeTrue();
            firstParticipant.TryGetProperty("TeamAssignments", out _).Should().BeTrue();
        }

        Console.WriteLine($"✅ Retrieved detailed data for {participants.GetArrayLength()} participants");
    }

    [Fact]
    public async Task CleanupTestAuctions_ShouldRemoveTestDataSafely()
    {
        // Arrange - First get count of auctions before cleanup
        var beforeResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auctions");
        var beforeContent = await beforeResponse.Content.ReadAsStringAsync();
        var beforeData = JsonSerializer.Deserialize<JsonElement>(beforeContent);
        var beforeCount = beforeData.GetProperty("TotalAuctions").GetInt32();

        // Act
        var response = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/cleanup-test-auctions", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(content);

        data.TryGetProperty("success", out var success).Should().BeTrue();
        success.GetBoolean().Should().BeTrue();

        data.TryGetProperty("deletedCount", out var deletedCount).Should().BeTrue();

        Console.WriteLine($"✅ Cleaned up {deletedCount.GetInt32()} test auctions");

        // Verify cleanup worked
        var afterResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auctions");
        var afterContent = await afterResponse.Content.ReadAsStringAsync();
        var afterData = JsonSerializer.Deserialize<JsonElement>(afterContent);
        var afterCount = afterData.GetProperty("TotalAuctions").GetInt32();

        (afterCount <= beforeCount).Should().BeTrue("Cleanup should reduce or maintain auction count");
        Console.WriteLine($"✅ Auction count reduced from {beforeCount} to {afterCount}");
    }

    [Fact]
    public async Task ResetAuction_ShouldClearParticipantsButKeepStructure()
    {
        // Arrange - Create test auction first
        var createResponse = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/create-test-data", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createData = JsonSerializer.Deserialize<JsonElement>(createContent);
        var auctionId = createData.GetProperty("auctionId").GetInt32();

        // Verify auction has participants before reset
        var beforeResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        var beforeContent = await beforeResponse.Content.ReadAsStringAsync();
        var beforeData = JsonSerializer.Deserialize<JsonElement>(beforeContent);
        var beforeParticipants = beforeData.GetProperty("participants").GetArrayLength();

        beforeParticipants.Should().BeGreaterThan(0);

        // Act - Reset the auction
        var resetResponse = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/reset", null);

        // Assert
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resetContent = await resetResponse.Content.ReadAsStringAsync();
        var resetData = JsonSerializer.Deserialize<JsonElement>(resetContent);

        resetData.TryGetProperty("success", out var success).Should().BeTrue();
        success.GetBoolean().Should().BeTrue();

        Console.WriteLine($"✅ Reset auction {auctionId} successfully");

        // Verify auction still exists but participants are cleared
        var afterResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        var afterContent = await afterResponse.Content.ReadAsStringAsync();
        var afterData = JsonSerializer.Deserialize<JsonElement>(afterContent);
        var afterParticipants = afterData.GetProperty("participants").GetArrayLength();

        afterParticipants.Should().Be(0);
        Console.WriteLine($"✅ Participants cleared: {beforeParticipants} → {afterParticipants}");
    }

    [Fact]
    public async Task DeleteAuction_ShouldCompletelyRemoveAuction()
    {
        // Arrange - Create test auction first
        var createResponse = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/create-test-data", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createData = JsonSerializer.Deserialize<JsonElement>(createContent);
        var auctionId = createData.GetProperty("auctionId").GetInt32();

        // Verify auction exists
        var beforeResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Delete the auction
        var deleteResponse = await _httpClient.DeleteAsync($"{BaseUrl}/diagnostic/auction/{auctionId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteContent = await deleteResponse.Content.ReadAsStringAsync();
        var deleteData = JsonSerializer.Deserialize<JsonElement>(deleteContent);

        deleteData.TryGetProperty("success", out var success).Should().BeTrue();
        success.GetBoolean().Should().BeTrue();

        Console.WriteLine($"✅ Deleted auction {auctionId} successfully");

        // Verify auction no longer exists
        var afterResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        Console.WriteLine($"✅ Auction {auctionId} no longer exists");
    }

    [Fact]
    public async Task FixDuplicateTeamIds_ShouldResolveTeamIdConflicts()
    {
        // Arrange - Use auction 37 which had duplicate team issues
        int testAuctionId = 37;

        // Act
        var response = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/auction/{testAuctionId}/fix-duplicate-teams", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(content);

        data.TryGetProperty("success", out var success).Should().BeTrue();
        success.GetBoolean().Should().BeTrue();

        Console.WriteLine($"✅ Fixed duplicate team IDs for auction {testAuctionId}");

        // Verify teams are now unique by checking team management endpoint
        var teamsResponse = await _httpClient.GetAsync($"{BaseUrl}/management/auctions/{testAuctionId}/teams");
        if (teamsResponse.IsSuccessStatusCode)
        {
            var teamsContent = await teamsResponse.Content.ReadAsStringAsync();
            var teamsData = JsonSerializer.Deserialize<JsonElement>(teamsContent);

            if (teamsData.TryGetProperty("Teams", out var teams))
            {
                var teamIds = new List<int>();
                foreach (var team in teams.EnumerateArray())
                {
                    if (team.TryGetProperty("TeamId", out var teamId))
                    {
                        teamIds.Add(teamId.GetInt32());
                    }
                }

                var uniqueTeamIds = teamIds.Distinct().ToList();
                uniqueTeamIds.Count.Should().Be(teamIds.Count, "All team IDs should be unique after fix");

                Console.WriteLine($"✅ Verified {uniqueTeamIds.Count} unique team IDs");
            }
        }
    }

    [Fact]
    public async Task DiagnosticEndpoints_ShouldRequireAuthentication()
    {
        // Arrange - Create client without management token
        using var unauthenticatedClient = new HttpClient();

        // Act & Assert - All diagnostic endpoints should return Unauthorized
        var endpoints = new[]
        {
            "/diagnostic/auctions",
            "/diagnostic/create-test-data",
            "/diagnostic/cleanup-test-auctions"
        };

        foreach (var endpoint in endpoints)
        {
            var response = endpoint.Contains("create-test-data") || endpoint.Contains("cleanup")
                ? await unauthenticatedClient.PostAsync($"{BaseUrl}{endpoint}", null)
                : await unauthenticatedClient.GetAsync($"{BaseUrl}{endpoint}");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"Endpoint {endpoint} should require authentication");

            Console.WriteLine($"✅ Endpoint {endpoint} properly secured");
        }
    }

    [Fact]
    public async Task SystemAdminWorkflow_ShouldSupportCompleteTestCycle()
    {
        Console.WriteLine("=== TESTING COMPLETE SYSTEM ADMIN WORKFLOW ===");

        // Step 1: Get initial state
        Console.WriteLine("Step 1: Getting initial auction summary");
        var initialResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auctions");
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initialContent = await initialResponse.Content.ReadAsStringAsync();
        var initialData = JsonSerializer.Deserialize<JsonElement>(initialContent);
        var initialCount = initialData.GetProperty("TotalAuctions").GetInt32();
        Console.WriteLine($"Initial auction count: {initialCount}");

        // Step 2: Create test data
        Console.WriteLine("\nStep 2: Creating test auction");
        var createResponse = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/create-test-data", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createData = JsonSerializer.Deserialize<JsonElement>(createContent);
        var auctionId = createData.GetProperty("auctionId").GetInt32();
        Console.WriteLine($"Created test auction: {auctionId}");

        // Step 3: Verify participants
        Console.WriteLine("\nStep 3: Verifying participant data");
        var participantsResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        participantsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var participantsContent = await participantsResponse.Content.ReadAsStringAsync();
        var participantsData = JsonSerializer.Deserialize<JsonElement>(participantsContent);
        var participantCount = participantsData.GetProperty("participants").GetArrayLength();
        participantCount.Should().BeGreaterThan(0);
        Console.WriteLine($"Auction has {participantCount} participants");

        // Step 4: Reset auction
        Console.WriteLine("\nStep 4: Resetting auction");
        var resetResponse = await _httpClient.PostAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/reset", null);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        Console.WriteLine("Auction reset successfully");

        // Step 5: Verify reset worked
        Console.WriteLine("\nStep 5: Verifying reset");
        var afterResetResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        var afterResetContent = await afterResetResponse.Content.ReadAsStringAsync();
        var afterResetData = JsonSerializer.Deserialize<JsonElement>(afterResetContent);
        var afterResetCount = afterResetData.GetProperty("participants").GetArrayLength();
        afterResetCount.Should().Be(0);
        Console.WriteLine($"Participants after reset: {afterResetCount}");

        // Step 6: Delete auction
        Console.WriteLine("\nStep 6: Deleting test auction");
        var deleteResponse = await _httpClient.DeleteAsync($"{BaseUrl}/diagnostic/auction/{auctionId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        Console.WriteLine("Auction deleted successfully");

        // Step 7: Verify deletion
        Console.WriteLine("\nStep 7: Verifying deletion");
        var finalResponse = await _httpClient.GetAsync($"{BaseUrl}/diagnostic/auction/{auctionId}/participants");
        finalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Console.WriteLine("Auction no longer exists - cleanup complete");

        Console.WriteLine("\n✅ COMPLETE WORKFLOW TEST PASSED");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Expected behavior documentation for System Admin Panel:
///
/// 1. AUTHENTICATION REQUIRED
///    - All diagnostic endpoints require X-Management-Token header
///    - Page redirects to login if no valid token in localStorage
///    - Returns 401 Unauthorized for invalid/missing tokens
///
/// 2. AUCTION INFORMATION SECTION
///    - "Get Auction Summary" returns all auctions with metadata
///    - Shows: AuctionId, Name, JoinCode, Status, ParticipantCount, TeamCount, CreatedDate
///    - Results displayed in formatted JSON for admin review
///
/// 3. DATA MANAGEMENT SECTION
///    - "Create Test Data": Creates DEBUG auction with sample participants and team assignments
///    - "Fix Duplicate Team IDs": Requires auction ID input, resolves team ID conflicts
///    - "Cleanup Test Auctions": Removes test/debug auctions safely (preserves production data)
///
/// 4. AUCTION OPERATIONS SECTION
///    - All operations require specific Auction ID input
///    - "Get Participants": Shows detailed participant and role assignment data
///    - "Reset Auction": Clears participants/roles but preserves auction structure
///    - "Delete Auction": Complete removal with confirmation dialog (irreversible)
///
/// 5. USER EXPERIENCE
///    - Loading indicators for all operations
///    - Success/error status messages with clear feedback
///    - Formatted JSON responses for technical review
///    - Input validation (auction ID required for operations)
///    - Confirmation dialogs for destructive operations
///
/// 6. SECURITY CONSIDERATIONS
///    - Management token validation on all endpoints
///    - No anonymous access to diagnostic functions
///    - Audit logging of all administrative actions
///    - Separate from user-facing auction functionality
/// </summary>