using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Handles authentication for management operations.
/// Provides token-based authentication with master password validation.
/// </summary>
public class ManagementAuthFunction(ILogger<ManagementAuthFunction> logger)
{
    private readonly ILogger<ManagementAuthFunction> _logger = logger;
    
    // TODO: Move to Azure Key Vault in production
    private const string MASTER_PASSWORD = "LeagifyAdmin2024!";
    private const int TOKEN_EXPIRY_HOURS = 8;

    /// <summary>
    /// Authenticates management user with master password and returns JWT-like token.
    /// </summary>
    [Function("AuthenticateManagement")]
    public async Task<HttpResponseData> AuthenticateManagement(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auth/login")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Management authentication request received");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Empty request body");
                return badRequestResponse;
            }

            var loginRequest = JsonSerializer.Deserialize<LoginRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (loginRequest == null || string.IsNullOrEmpty(loginRequest.Password))
            {
                var badDataResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badDataResponse.WriteStringAsync("Invalid request format or missing password");
                return badDataResponse;
            }

            // Validate master password
            if (loginRequest.Password != MASTER_PASSWORD)
            {
                _logger.LogWarning("Invalid management password attempt from {UserAgent}", 
                    req.Headers.FirstOrDefault(h => h.Key == "User-Agent").Value?.FirstOrDefault());
                
                // Add small delay to prevent brute force attacks
                await Task.Delay(1000);
                
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid credentials");
                return unauthorizedResponse;
            }

            // Generate authentication token
            var expiryTime = DateTime.UtcNow.AddHours(TOKEN_EXPIRY_HOURS);
            var tokenData = $"admin:{expiryTime:yyyy-MM-ddTHH:mm:ssZ}";
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
            
            Console.WriteLine($"[DEBUG] Login - Generated token data: {tokenData}");
            Console.WriteLine($"[DEBUG] Login - Generated token: {token}");
            Console.WriteLine($"[DEBUG] Login - Token length: {token.Length}");

            _logger.LogInformation("Successful management authentication. Token expires at {ExpiryTime}", expiryTime);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                token = token,
                expiresAt = expiryTime,
                message = "Authentication successful"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during management authentication");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Authentication service error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Validates an existing management token and returns user info.
    /// </summary>
    [Function("ValidateManagementToken")]
    public async Task<HttpResponseData> ValidateToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "management/auth/validate")] HttpRequestData req)
    {
        try
        {
            var tokenValidation = ValidateManagementToken(req);
            
            if (!tokenValidation.IsValid)
            {
                _logger.LogWarning("Token validation failed: {ErrorMessage}", tokenValidation.ErrorMessage);
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync(tokenValidation.ErrorMessage ?? "Invalid token");
                return unauthorizedResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                valid = true,
                expiresAt = tokenValidation.ExpiryTime,
                role = "admin"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating management token");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Token validation error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Logs out management user by invalidating token (client-side cleanup).
    /// </summary>
    [Function("LogoutManagement")]
    public async Task<HttpResponseData> LogoutManagement(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "management/auth/logout")] HttpRequestData req)
    {
        try
        {
            // For stateless tokens, logout is primarily client-side
            // In production, consider maintaining a token blacklist in Redis/Database
            
            _logger.LogInformation("Management logout request received");
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                message = "Logged out successfully"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during management logout");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Logout service error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Validates a management token from the Authorization header.
    /// Used internally by other management functions.
    /// </summary>
    public static TokenValidationResult ValidateManagementToken(HttpRequestData req)
    {
        try
        {
            // Check for Authorization header
            if (!req.Headers.TryGetValues("Authorization", out var authHeaderValues))
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Missing Authorization header" };
            }

            var authHeader = authHeaderValues.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Invalid Authorization header format" };
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            Console.WriteLine($"[DEBUG] Token received: '{token}' (length: {token.Length})");

            // Decode and validate token
            byte[] decodedBytes;
            string decodedString;
            try
            {
                decodedBytes = Convert.FromBase64String(token);
                decodedString = Encoding.UTF8.GetString(decodedBytes);
                Console.WriteLine($"[DEBUG] Decoded token: {decodedString}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Base64 decode failed: {ex.Message}");
                Console.WriteLine($"[DEBUG] Token length: {token.Length}, Token: '{token}'");
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Invalid token encoding" };
            }
            
            // Split only on the first colon to separate "admin" from the datetime
            var colonIndex = decodedString.IndexOf(':');
            if (colonIndex == -1)
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Invalid token format" };
            }
            
            var parts = new string[]
            {
                decodedString.Substring(0, colonIndex),
                decodedString.Substring(colonIndex + 1)
            };

            Console.WriteLine($"[DEBUG] Token parts: '{parts[0]}', '{parts[1]}'");
            if (parts.Length != 2 || parts[0] != "admin")
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Invalid token format" };
            }
            
            // Use explicit format parsing for ISO 8601 dates
            Console.WriteLine($"[DEBUG] Attempting to parse datetime: '{parts[1]}'");
            if (!DateTime.TryParseExact(parts[1], "yyyy-MM-ddTHH:mm:ssZ", 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, 
                out var expiryTime))
            {
                Console.WriteLine($"[DEBUG] Exact parse failed, trying fallback parse");
                // Fallback to general parsing
                if (!DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out expiryTime))
                {
                    Console.WriteLine($"[DEBUG] Fallback parse also failed");
                    return new TokenValidationResult { IsValid = false, ErrorMessage = "Invalid token expiry format" };
                }
                Console.WriteLine($"[DEBUG] Fallback parse succeeded: {expiryTime}");
            }
            else
            {
                Console.WriteLine($"[DEBUG] Exact parse succeeded: {expiryTime}");
            }

            Console.WriteLine($"[DEBUG] Parsed expiry: {expiryTime}, Current UTC: {DateTime.UtcNow}");
            if (DateTime.UtcNow >= expiryTime)
            {
                Console.WriteLine($"[DEBUG] Token expired");
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Token has expired" };
            }

            Console.WriteLine($"[DEBUG] Token validation successful");
            return new TokenValidationResult 
            { 
                IsValid = true, 
                ExpiryTime = expiryTime,
                Role = "admin"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Token validation exception: {ex.Message}");
            return new TokenValidationResult { IsValid = false, ErrorMessage = "Token validation failed" };
        }
    }
}

/// <summary>
/// Request model for management login.
/// </summary>
public class LoginRequest
{
    public string Password { get; set; } = "";
}

/// <summary>
/// Result of token validation.
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ExpiryTime { get; set; }
    public string? Role { get; set; }
}