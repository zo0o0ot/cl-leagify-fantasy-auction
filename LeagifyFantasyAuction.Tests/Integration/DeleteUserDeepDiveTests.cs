using System.Text;
using System.Text.Json;
using FluentAssertions;
using System.Net;

namespace LeagifyFantasyAuction.Tests.Integration;

/// <summary>
/// Deep dive test to isolate exactly where DELETE user is failing
/// Testing each step of the delete process individually
/// </summary>
public class DeleteUserDeepDiveTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api";
    private const string ManagementToken = "leagify-admin-2024";

    public DeleteUserDeepDiveTests()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Management-Token", ManagementToken);
    }

    [Fact]
    public async Task Step1_TestManagementTokenValidation()
    {
        Console.WriteLine("=== TESTING MANAGEMENT TOKEN VALIDATION ===");

        // Test with a working management endpoint - teams API
        int auctionId = 37;

        Console.WriteLine("Step 1A: Test with management token present");
        var responseWithToken = await _httpClient.GetAsync($"{BaseUrl}/management/auctions/{auctionId}/teams");
        Console.WriteLine($"Response Status: {responseWithToken.StatusCode}");
        var responseText = await responseWithToken.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseText.Substring(0, Math.Min(100, responseText.Length))}...");

        // Test without token
        Console.WriteLine("\nStep 1B: Test without management token");
        var clientNoToken = new HttpClient();
        var responseNoToken = await clientNoToken.GetAsync($"{BaseUrl}/management/auctions/{auctionId}/teams");
        Console.WriteLine($"Response Status: {responseNoToken.StatusCode}");
        var responseNoTokenText = await responseNoToken.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseNoTokenText}");

        clientNoToken.Dispose();

        // If the teams API works with the token, the delete API should too
        responseWithToken.StatusCode.Should().Be(HttpStatusCode.OK, "Management token should be valid");
    }

    [Fact]
    public async Task Step2_TestDeleteUserStepByStep()
    {
        Console.WriteLine("=== TESTING DELETE USER STEP BY STEP ===");

        int auctionId = 37;
        int userId = 14; // FF inco test 4 (has 0 roles)

        Console.WriteLine("Step 2A: Verify auction exists");
        var participantsCheck = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        Console.WriteLine($"Participants API Status: {participantsCheck.StatusCode}");
        participantsCheck.StatusCode.Should().Be(HttpStatusCode.OK, "Auction should exist");

        Console.WriteLine("\nStep 2B: Verify user exists in auction");
        var participantsText = await participantsCheck.Content.ReadAsStringAsync();
        var participants = JsonSerializer.Deserialize<JsonElement[]>(participantsText);
        var targetUser = participants.FirstOrDefault(p => p.GetProperty("UserId").GetInt32() == userId);
        targetUser.ValueKind.Should().NotBe(JsonValueKind.Undefined, "User should exist in auction");
        Console.WriteLine($"Found user: {targetUser.GetProperty("DisplayName").GetString()}");

        Console.WriteLine("\nStep 2C: Attempt DELETE with detailed monitoring");
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/management/auctions/{auctionId}/users/{userId}");
        deleteRequest.Headers.Add("X-Management-Token", ManagementToken);

        var deleteResponse = await _httpClient.SendAsync(deleteRequest);
        Console.WriteLine($"DELETE Status: {deleteResponse.StatusCode}");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"DELETE Body: {deleteBody}");

        // Check response headers for additional info
        Console.WriteLine("\nResponse Headers:");
        foreach (var header in deleteResponse.Headers)
        {
            Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        }
        foreach (var header in deleteResponse.Content.Headers)
        {
            Console.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        }
    }

    [Fact]
    public async Task Step3_TestDifferentUser()
    {
        Console.WriteLine("=== TESTING DELETE DIFFERENT USER ===");

        int auctionId = 37;

        // Get current participants to find a different user to test
        var participantsResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
        var participantsText = await participantsResponse.Content.ReadAsStringAsync();
        var participants = JsonSerializer.Deserialize<JsonElement[]>(participantsText);

        Console.WriteLine("Available users for testing:");
        foreach (var participant in participants)
        {
            var userId = participant.GetProperty("UserId").GetInt32();
            var displayName = participant.GetProperty("DisplayName").GetString();
            var isConnected = participant.GetProperty("IsConnected").GetBoolean();
            var rolesCount = participant.GetProperty("Roles").GetArrayLength();

            Console.WriteLine($"  User {userId}: {displayName} (Connected: {isConnected}, Roles: {rolesCount})");

            // Test delete on the InternalTestUser (should be safe to delete)
            if (displayName == "InternalTestUser")
            {
                Console.WriteLine($"\nTesting DELETE on InternalTestUser (ID: {userId})");
                var deleteResponse = await _httpClient.DeleteAsync($"{BaseUrl}/management/auctions/{auctionId}/users/{userId}");
                Console.WriteLine($"DELETE Status: {deleteResponse.StatusCode}");
                var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"DELETE Body: {deleteBody}");

                // Check if user still exists
                var afterDeleteResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
                var afterDeleteText = await afterDeleteResponse.Content.ReadAsStringAsync();
                var afterParticipants = JsonSerializer.Deserialize<JsonElement[]>(afterDeleteText);
                var userStillExists = afterParticipants.Any(p => p.GetProperty("UserId").GetInt32() == userId);
                Console.WriteLine($"User still exists after delete: {userStillExists}");

                break;
            }
        }
    }

    [Fact]
    public async Task Step4_TestAuthenticationVariations()
    {
        Console.WriteLine("=== TESTING AUTHENTICATION VARIATIONS ===");

        int auctionId = 37;
        int userId = 14;

        Console.WriteLine("Step 4A: Test with different token values");
        var tokenVariations = new[]
        {
            "leagify-admin-2024",
            "wrong-token",
            "",
            "leagify-admin"
        };

        foreach (var token in tokenVariations)
        {
            Console.WriteLine($"\nTesting with token: '{token}'");
            var client = new HttpClient();
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Add("X-Management-Token", token);
            }

            var response = await client.DeleteAsync($"{BaseUrl}/management/auctions/{auctionId}/users/{userId}");
            Console.WriteLine($"Status: {response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Body: {body}");

            client.Dispose();
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}