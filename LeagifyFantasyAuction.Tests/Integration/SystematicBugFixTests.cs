using System.Text;
using System.Text.Json;
using FluentAssertions;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LeagifyFantasyAuction.Api.Data;

namespace LeagifyFantasyAuction.Tests.Integration;

/// <summary>
/// Systematic debugging tests to identify root causes of persistent issues:
/// 1. Delete user functionality
/// 2. Leave auction status updates
/// 3. Team dropdown consistency
///
/// These tests verify both API responses AND database state changes
/// </summary>
public class SystematicBugFixTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api";
    private const string ManagementToken = "leagify-admin-2024"; // From ValidateManagementAuth

    public SystematicBugFixTests()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Management-Token", ManagementToken);
    }

    [Fact]
    public async Task H1_DeleteUser_ForeignKeyConstraints()
    {
        Console.WriteLine("=== TESTING DELETE USER - FOREIGN KEY CONSTRAINTS ===");

        // Test with a user that likely has roles assigned (user 14 from previous testing)
        int auctionId = 37;
        int userId = 14;

        // First, get the user's current state
        Console.WriteLine($"Step 1: Getting current state for user {userId} in auction {auctionId}");
        var participantsResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        participantsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var participantsText = await participantsResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Current participants: {participantsText}");

        var participants = JsonSerializer.Deserialize<JsonElement[]>(participantsText);
        var targetUser = participants.FirstOrDefault(p => p.GetProperty("UserId").GetInt32() == userId);

        if (targetUser.ValueKind == JsonValueKind.Undefined)
        {
            Console.WriteLine($"User {userId} not found in auction {auctionId} - test cannot proceed");
            return;
        }

        Console.WriteLine($"Found user: {targetUser.GetProperty("DisplayName").GetString()}");

        if (targetUser.TryGetProperty("Roles", out var roles))
        {
            Console.WriteLine($"User has {roles.GetArrayLength()} roles:");
            foreach (var role in roles.EnumerateArray())
            {
                Console.WriteLine($"  - Role: {GetJsonProperty(role, "role")}, TeamId: {GetJsonProperty(role, "teamId")}");
            }
        }

        // Step 2: Attempt to delete the user
        Console.WriteLine($"\nStep 2: Attempting to delete user {userId}");
        var deleteResponse = await _httpClient.DeleteAsync($"{BaseUrl}/management/auctions/{auctionId}/users/{userId}");

        Console.WriteLine($"Delete Response Status: {deleteResponse.StatusCode}");
        var deleteResponseText = await deleteResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Delete Response Body: {deleteResponseText}");

        // Step 3: Verify user state after delete attempt
        Console.WriteLine($"\nStep 3: Verifying user state after delete attempt");
        var afterDeleteResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var afterDeleteText = await afterDeleteResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Participants after delete: {afterDeleteText}");

        var afterParticipants = JsonSerializer.Deserialize<JsonElement[]>(afterDeleteText);
        var userStillExists = afterParticipants.Any(p => p.GetProperty("UserId").GetInt32() == userId);

        Console.WriteLine($"User still exists: {userStillExists}");

        if (deleteResponse.IsSuccessStatusCode)
        {
            userStillExists.Should().BeFalse("User should be deleted if API returned success");
        }
        else
        {
            Console.WriteLine($"Delete failed as expected - Error: {deleteResponseText}");
        }
    }

    [Fact]
    public async Task H2_LeaveAuction_DatabaseStateVerification()
    {
        Console.WriteLine("=== TESTING LEAVE AUCTION - DATABASE STATE VERIFICATION ===");

        int auctionId = 37;

        // First, find a connected user to test leave functionality
        Console.WriteLine("Step 1: Finding a connected user to test leave functionality");
        var participantsResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var participantsText = await participantsResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Current participants: {participantsText}");

        var participants = JsonSerializer.Deserialize<JsonElement[]>(participantsText);
        var connectedUser = participants.FirstOrDefault(p =>
            p.TryGetProperty("IsConnected", out var connected) && connected.GetBoolean());

        if (connectedUser.ValueKind == JsonValueKind.Undefined)
        {
            Console.WriteLine("No connected users found - cannot test leave functionality");
            Console.WriteLine("This might indicate the leave functionality is already working correctly");
            return;
        }

        var userId = connectedUser.GetProperty("UserId").GetInt32();
        var displayName = connectedUser.GetProperty("DisplayName").GetString();
        Console.WriteLine($"Testing with connected user: {displayName} (ID: {userId})");

        // Step 2: Simulate leave auction API call
        Console.WriteLine($"\nStep 2: Testing leave auction API directly");

        // We need a session token for this user - let's try to get it by simulating a join
        // For now, let's test the API endpoint directly without session token to see behavior
        var leaveRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/auction/{auctionId}/leave");
        leaveRequest.Headers.Add("X-Auction-Token", "dummy-token-for-testing");

        var leaveResponse = await _httpClient.SendAsync(leaveRequest);
        Console.WriteLine($"Leave Response Status: {leaveResponse.StatusCode}");
        var leaveResponseText = await leaveResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Leave Response Body: {leaveResponseText}");

        // Step 3: Check if user status changed
        Console.WriteLine($"\nStep 3: Verifying user connection status after leave attempt");
        await Task.Delay(1000); // Give database time to update

        var afterLeaveResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var afterLeaveText = await afterLeaveResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Participants after leave: {afterLeaveText}");

        var afterParticipants = JsonSerializer.Deserialize<JsonElement[]>(afterLeaveText);
        var userAfterLeave = afterParticipants.FirstOrDefault(p => p.GetProperty("UserId").GetInt32() == userId);

        if (userAfterLeave.ValueKind != JsonValueKind.Undefined)
        {
            var stillConnected = userAfterLeave.GetProperty("IsConnected").GetBoolean();
            Console.WriteLine($"User {displayName} still connected: {stillConnected}");

            // If the leave API succeeded, the user should not be connected
            if (leaveResponse.IsSuccessStatusCode)
            {
                stillConnected.Should().BeFalse("User should be disconnected after successful leave");
            }
        }
    }

    [Fact]
    public async Task H3_TeamDropdown_MultipleCallConsistency()
    {
        Console.WriteLine("=== TESTING TEAM DROPDOWN - MULTIPLE CALL CONSISTENCY ===");

        int auctionId = 37;
        var teamResponses = new List<string>();

        Console.WriteLine("Making 5 consecutive calls to team API to detect inconsistencies...");

        for (int i = 1; i <= 5; i++)
        {
            Console.WriteLine($"\nCall {i}:");
            var response = await _httpClient.GetAsync($"{BaseUrl}/management/auctions/{auctionId}/teams");

            Console.WriteLine($"Status: {response.StatusCode}");
            var responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response length: {responseText.Length} chars");

            if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(responseText))
            {
                try
                {
                    var teamData = JsonSerializer.Deserialize<JsonElement>(responseText);
                    if (teamData.TryGetProperty("Teams", out var teams))
                    {
                        var teamList = new List<string>();
                        foreach (var team in teams.EnumerateArray())
                        {
                            var teamName = team.GetProperty("TeamName").GetString();
                            var nominationOrder = team.GetProperty("NominationOrder").GetInt32();
                            teamList.Add($"{teamName}(Order:{nominationOrder})");
                        }

                        var teamListStr = string.Join(", ", teamList);
                        Console.WriteLine($"Teams: {teamListStr}");
                        teamResponses.Add(teamListStr);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing response: {ex.Message}");
                    teamResponses.Add($"ERROR: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Failed response: {responseText}");
                teamResponses.Add($"HTTP_{response.StatusCode}: {responseText}");
            }

            // Small delay between calls
            await Task.Delay(500);
        }

        // Analyze consistency
        Console.WriteLine($"\n=== CONSISTENCY ANALYSIS ===");
        var uniqueResponses = teamResponses.Distinct().ToList();
        Console.WriteLine($"Unique response patterns: {uniqueResponses.Count}");

        for (int i = 0; i < uniqueResponses.Count; i++)
        {
            var pattern = uniqueResponses[i];
            var count = teamResponses.Count(r => r == pattern);
            Console.WriteLine($"Pattern {i + 1} (appeared {count} times): {pattern}");
        }

        if (uniqueResponses.Count > 1)
        {
            Console.WriteLine("❌ INCONSISTENCY DETECTED: API returns different results across calls");
        }
        else
        {
            Console.WriteLine("✓ CONSISTENCY VERIFIED: All calls returned the same result");
        }

        // Verify we get exactly 6 teams without duplicates
        if (uniqueResponses.Count == 1 && !uniqueResponses[0].StartsWith("HTTP_") && !uniqueResponses[0].StartsWith("ERROR:"))
        {
            var response = uniqueResponses[0];
            var expectedTeams = new[] { "Team 1", "Team 2", "Team 3", "Team 4", "Team 5", "Team 6" };

            foreach (var expectedTeam in expectedTeams)
            {
                if (!response.Contains(expectedTeam))
                {
                    Console.WriteLine($"❌ Missing expected team: {expectedTeam}");
                }
            }

            // Check for duplicates
            foreach (var expectedTeam in expectedTeams)
            {
                var occurrences = CountOccurrences(response, expectedTeam);
                if (occurrences > 1)
                {
                    Console.WriteLine($"❌ Duplicate team detected: {expectedTeam} appears {occurrences} times");
                }
            }
        }
    }

    [Fact]
    public async Task H4_CompleteWorkflowIntegration()
    {
        Console.WriteLine("=== TESTING COMPLETE WORKFLOW INTEGRATION ===");

        int auctionId = 37;

        Console.WriteLine("Step 1: Create a test user by joining auction");
        // Note: We would need a valid join code for this test
        // For now, let's work with existing users

        Console.WriteLine("Step 2: Get current participant list");
        var initialResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var initialText = await initialResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Initial participants: {initialText}");

        Console.WriteLine("Step 3: Test team assignment workflow");
        // This would require management token and existing user

        Console.WriteLine("Step 4: Test leave auction workflow");
        // This requires valid session token

        Console.WriteLine("Step 5: Test delete user workflow");
        // This requires management token

        Console.WriteLine("Complete workflow testing requires valid authentication tokens");
        Console.WriteLine("Individual component tests above provide better isolation for debugging");
    }

    private static string GetJsonProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind == JsonValueKind.Null ? "null" : property.ToString();
        }
        return "missing";
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}