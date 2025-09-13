using System.Text;
using System.Text.Json;
using FluentAssertions;
using System.Net;

namespace LeagifyFantasyAuction.Tests.Integration;

/// <summary>
/// Tests to investigate why user 19 cannot be deleted even after removing team assignments
/// This will help determine if there are database constraints we need to handle
/// </summary>
public class DeleteConstraintTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://jolly-meadow-0b4450210.2.azurestaticapps.net/api";
    private const string ManagementToken = "leagify-admin-2024";

    public DeleteConstraintTests()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-Management-Token", ManagementToken);
    }

    [Fact]
    public async Task InvestigateUser19DeletionIssue()
    {
        Console.WriteLine("=== INVESTIGATING USER 19 DELETION CONSTRAINT ===");

        int auctionId = 37;
        int userId = 19;

        // Step 1: Check if user 19 exists and get current state
        Console.WriteLine($"Step 1: Checking current state of user {userId} in auction {auctionId}");
        var participantsResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");

        if (!participantsResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Cannot get participants: {participantsResponse.StatusCode}");
            return;
        }

        var participantsText = await participantsResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Current participants: {participantsText}");

        var participants = JsonSerializer.Deserialize<JsonElement[]>(participantsText);
        var user19 = participants?.FirstOrDefault(p => p.GetProperty("UserId").GetInt32() == userId);

        if (user19?.ValueKind == JsonValueKind.Undefined)
        {
            Console.WriteLine($"✅ User {userId} doesn't exist - may have been deleted or never existed");
            return;
        }

        Console.WriteLine($"Found User 19: {user19?.GetProperty("DisplayName").GetString()}");
        Console.WriteLine($"Connected: {user19?.GetProperty("IsConnected").GetBoolean()}");

        if (user19?.TryGetProperty("Roles", out var roles) == true)
        {
            Console.WriteLine($"Roles: {roles.GetArrayLength()}");
            foreach (var role in roles.EnumerateArray())
            {
                Console.WriteLine($"  - Role: {GetJsonProperty(role, "Role")}, TeamId: {GetJsonProperty(role, "TeamId")}, TeamName: {GetJsonProperty(role, "TeamName")}");
            }
        }

        // Step 2: Try to delete the user
        Console.WriteLine($"\nStep 2: Attempting to delete user {userId}");
        var deleteResponse = await _httpClient.DeleteAsync($"{BaseUrl}/management/auctions/{auctionId}/users/{userId}");

        Console.WriteLine($"Delete Response Status: {deleteResponse.StatusCode}");
        var deleteResponseText = await deleteResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"Delete Response Body: {deleteResponseText}");

        // Step 3: If delete failed, let's investigate why
        if (!deleteResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"\nStep 3: Investigating why deletion failed");

            // Check if there are any UserRoles still associated
            Console.WriteLine("Checking participant data again after failed delete:");
            var afterDeleteResponse = await _httpClient.GetAsync($"{BaseUrl}/auction/{auctionId}/participants");
            if (afterDeleteResponse.IsSuccessStatusCode)
            {
                var afterDeleteText = await afterDeleteResponse.Content.ReadAsStringAsync();
                var afterParticipants = JsonSerializer.Deserialize<JsonElement[]>(afterDeleteText);
                var stillExists = afterParticipants?.FirstOrDefault(p => p.GetProperty("UserId").GetInt32() == userId);

                if (stillExists?.ValueKind != JsonValueKind.Undefined)
                {
                    Console.WriteLine("User still exists with the following data:");
                    Console.WriteLine($"  DisplayName: {stillExists?.GetProperty("DisplayName").GetString()}");
                    Console.WriteLine($"  IsConnected: {stillExists?.GetProperty("IsConnected").GetBoolean()}");
                    Console.WriteLine($"  Roles: {stillExists?.GetProperty("Roles").GetArrayLength()}");

                    if (stillExists?.TryGetProperty("Roles", out var stillRoles) == true && stillRoles.GetArrayLength() > 0)
                    {
                        Console.WriteLine("❌ PROBLEM: User still has roles after attempted deletion!");
                        foreach (var role in stillRoles.EnumerateArray())
                        {
                            Console.WriteLine($"    - Role: {GetJsonProperty(role, "Role")}, UserRoleId: {GetJsonProperty(role, "UserRoleId")}");
                        }
                    }
                }
            }

            // Test if we can delete users without any roles for comparison
            Console.WriteLine("\nStep 4: Testing if we can delete users without roles");
            var usersWithoutRoles = participants?.Where(p =>
            {
                if (p.TryGetProperty("Roles", out var userRoles))
                {
                    return userRoles.GetArrayLength() == 0;
                }
                return true;
            }).ToList();

            Console.WriteLine($"Found {usersWithoutRoles?.Count ?? 0} users without roles:");
            foreach (var user in usersWithoutRoles ?? new List<JsonElement>())
            {
                var uid = user.GetProperty("UserId").GetInt32();
                var name = user.GetProperty("DisplayName").GetString();
                Console.WriteLine($"  - User {uid}: {name}");

                if (uid != userId) // Don't try to delete the same problematic user
                {
                    Console.WriteLine($"    Testing delete on User {uid}...");
                    var testDeleteResponse = await _httpClient.DeleteAsync($"{BaseUrl}/management/auctions/{auctionId}/users/{uid}");
                    Console.WriteLine($"    Result: {testDeleteResponse.StatusCode}");
                    if (testDeleteResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"    ✅ User {uid} deleted successfully");
                        break; // Only test one to avoid deleting all test data
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"✅ User {userId} deleted successfully");
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