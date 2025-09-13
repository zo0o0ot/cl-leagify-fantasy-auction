using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace LeagifyFantasyAuction.Tests.Integration;

/// <summary>
/// Comprehensive debugging tests to systematically verify API behavior
/// and identify root causes of persistent issues with team assignments and leave auction
/// </summary>
public class SystematicApiDebuggingTests
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api";

    public SystematicApiDebuggingTests()
    {
        _httpClient = new HttpClient();
    }

    [Fact]
    public async Task Step1_VerifyParticipantApiResponseStructure()
    {
        // Test with auction ID 1 to see what data structure we get
        var response = await _httpClient.GetAsync($"{BaseUrl}/auction/1/participants");

        // Log the full response for analysis
        var responseText = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"=== PARTICIPANT API RESPONSE ===");
        Console.WriteLine($"Status Code: {response.StatusCode}");
        Console.WriteLine($"Response Body: {responseText}");
        Console.WriteLine($"Content-Type: {response.Content.Headers.ContentType}");

        if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(responseText))
        {
            try
            {
                var participants = JsonSerializer.Deserialize<JsonElement[]>(responseText);
                Console.WriteLine($"Participant Count: {participants.Length}");

                foreach (var participant in participants)
                {
                    Console.WriteLine($"--- Participant Details ---");
                    Console.WriteLine($"DisplayName: {GetJsonProperty(participant, "displayName")}");
                    Console.WriteLine($"IsConnected: {GetJsonProperty(participant, "isConnected")}");
                    Console.WriteLine($"UserId: {GetJsonProperty(participant, "userId")}");

                    if (participant.TryGetProperty("roles", out var roles))
                    {
                        Console.WriteLine($"Roles Count: {roles.GetArrayLength()}");
                        foreach (var role in roles.EnumerateArray())
                        {
                            Console.WriteLine($"  Role: {GetJsonProperty(role, "role")}");
                            Console.WriteLine($"  TeamId: {GetJsonProperty(role, "teamId")}");
                            Console.WriteLine($"  TeamName: {GetJsonProperty(role, "teamName")}");
                            Console.WriteLine($"  UserRoleId: {GetJsonProperty(role, "userRoleId")}");
                        }
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing participant data: {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task Step2_TestJoinAuctionFlow()
    {
        // Try joining with different common join codes to find working data
        var testJoinCodes = new[] { "DEMO", "TEST", "ALPHA", "BETA", "SAMPLE", "DEV" };

        foreach (var joinCode in testJoinCodes)
        {
            Console.WriteLine($"=== TESTING JOIN CODE: {joinCode} ===");

            var joinRequest = new
            {
                JoinCode = joinCode,
                DisplayName = $"DebugUser_{joinCode}"
            };

            var joinJson = JsonSerializer.Serialize(joinRequest);
            var joinContent = new StringContent(joinJson, Encoding.UTF8, "application/json");

            var joinResponse = await _httpClient.PostAsync($"{BaseUrl}/auction/join", joinContent);
            var joinResponseText = await joinResponse.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {joinResponse.StatusCode}");
            Console.WriteLine($"Response: {joinResponseText}");

            if (joinResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ SUCCESS: Found working join code: {joinCode}");
                try
                {
                    var joinResult = JsonSerializer.Deserialize<JsonElement>(joinResponseText);
                    var auctionId = joinResult.GetProperty("auctionId").GetInt32();
                    var sessionToken = joinResult.GetProperty("sessionToken").GetString();

                    Console.WriteLine($"AuctionId: {auctionId}");
                    Console.WriteLine($"SessionToken: {sessionToken}");

                    // Test leave auction immediately
                    await TestLeaveAuctionFlow(auctionId, sessionToken);
                    return; // Found working data, stop testing other codes
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing join response: {ex.Message}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine("❌ No working join codes found - need to create test data");
    }

    private async Task TestLeaveAuctionFlow(int auctionId, string sessionToken)
    {
        Console.WriteLine($"=== TESTING LEAVE AUCTION FLOW ===");

        // Get participants before leaving
        var beforeResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var beforeText = await beforeResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Participants BEFORE leave: {beforeText}");

        // Leave the auction
        var leaveRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/auction/{auctionId}/leave");
        leaveRequest.Headers.Add("X-Auction-Token", sessionToken);

        var leaveResponse = await _httpClient.SendAsync(leaveRequest);
        var leaveText = await leaveResponse.Content.ReadAsStringAsync();

        Console.WriteLine($"Leave Response Status: {leaveResponse.StatusCode}");
        Console.WriteLine($"Leave Response Body: {leaveText}");

        // Get participants after leaving
        var afterResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var afterText = await afterResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Participants AFTER leave: {afterText}");

        // Compare before and after
        if (beforeText != afterText)
        {
            Console.WriteLine("✓ SUCCESS: Participant data changed after leave");
        }
        else
        {
            Console.WriteLine("❌ PROBLEM: Participant data unchanged after leave");
        }
    }

    [Fact]
    public async Task Step3_TestManagementEndpoints()
    {
        Console.WriteLine($"=== TESTING MANAGEMENT ENDPOINTS ===");

        // Try to get auctions list (will likely fail due to auth)
        var auctionsResponse = await _httpClient.GetAsync($"{BaseUrl}/management/auctions");
        Console.WriteLine($"Auctions endpoint status: {auctionsResponse.StatusCode}");

        if (auctionsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("Expected: Management endpoints require authentication");
        }
        else
        {
            var auctionsText = await auctionsResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Auctions response: {auctionsText}");
        }
    }

    [Fact]
    public async Task Step4_TestTeamEndpoints()
    {
        Console.WriteLine($"=== TESTING TEAM ENDPOINTS ===");

        // Test team endpoints for auction 1
        var teamResponse = await _httpClient.GetAsync($"{BaseUrl}/management/auctions/1/teams");
        var teamText = await teamResponse.Content.ReadAsStringAsync();

        Console.WriteLine($"Teams endpoint status: {teamResponse.StatusCode}");
        Console.WriteLine($"Teams response: {teamText}");

        if (teamResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("Expected: Team management requires authentication");
        }
    }

    private static string GetJsonProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.ValueKind == JsonValueKind.Null ? "null" : property.ToString();
        }
        return "missing";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}