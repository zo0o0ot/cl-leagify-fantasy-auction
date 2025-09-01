using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace LeagifyFantasyAuction.Api.Functions;

public class AdminAuthFunction
{
    private readonly ILogger<AdminAuthFunction> _logger;

    public AdminAuthFunction(ILogger<AdminAuthFunction> logger)
    {
        _logger = logger;
    }

    [Function("AdminAuth")]
    public async Task<HttpResponseData> AdminAuth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "management/auth")] HttpRequestData req)
    {
        _logger.LogInformation("Admin authentication request received");

        try
        {
            // Read request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var authRequest = JsonSerializer.Deserialize<AdminAuthRequest>(requestBody);

            if (authRequest?.Password == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Password is required");
                return badResponse;
            }

            // Get master password from environment variables
            var masterPassword = Environment.GetEnvironmentVariable("ADMIN_MASTER_PASSWORD") ?? "DefaultAdminPassword123!";

            if (authRequest.Password == masterPassword)
            {
                // Generate a simple token (in production, use JWT)
                var adminToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"admin:{DateTime.UtcNow.AddHours(8):O}"));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(JsonSerializer.Serialize(new AdminAuthResponse
                {
                    Success = true,
                    Token = adminToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }));

                response.Headers.Add("Content-Type", "application/json");
                return response;
            }
            else
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync(JsonSerializer.Serialize(new AdminAuthResponse
                {
                    Success = false,
                    Message = "Invalid password"
                }));

                unauthorizedResponse.Headers.Add("Content-Type", "application/json");
                return unauthorizedResponse;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin authentication");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Authentication failed");
            return errorResponse;
        }
    }

    [Function("AdminValidateToken")]
    public async Task<HttpResponseData> ValidateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/validate")] HttpRequestData req)
    {
        _logger.LogInformation("Admin token validation request received");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var validateRequest = JsonSerializer.Deserialize<ValidateTokenRequest>(requestBody);

            if (validateRequest?.Token == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Token is required");
                return badResponse;
            }

            // Decode and validate token (simple implementation)
            try
            {
                var decodedBytes = Convert.FromBase64String(validateRequest.Token);
                var decodedString = System.Text.Encoding.UTF8.GetString(decodedBytes);
                var parts = decodedString.Split(':');

                if (parts.Length == 2 && parts[0] == "admin" && DateTime.TryParse(parts[1], out var expiryDate))
                {
                    if (DateTime.UtcNow < expiryDate)
                    {
                        var response = req.CreateResponse(HttpStatusCode.OK);
                        await response.WriteStringAsync(JsonSerializer.Serialize(new ValidateTokenResponse
                        {
                            Valid = true,
                            ExpiresAt = expiryDate
                        }));

                        response.Headers.Add("Content-Type", "application/json");
                        return response;
                    }
                }
            }
            catch (Exception)
            {
                // Invalid token format
            }

            var invalidResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
            await invalidResponse.WriteStringAsync(JsonSerializer.Serialize(new ValidateTokenResponse
            {
                Valid = false,
                Message = "Invalid or expired token"
            }));

            invalidResponse.Headers.Add("Content-Type", "application/json");
            return invalidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token validation");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Token validation failed");
            return errorResponse;
        }
    }
}

public class AdminAuthRequest
{
    public string? Password { get; set; }
}

public class AdminAuthResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Message { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class ValidateTokenRequest
{
    public string? Token { get; set; }
}

public class ValidateTokenResponse
{
    public bool Valid { get; set; }
    public string? Message { get; set; }
    public DateTime? ExpiresAt { get; set; }
}