using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Temporary debugging function to help diagnose token issues.
/// This should be removed after authentication is fixed.
/// </summary>
public class TokenDebugFunction(ILogger<TokenDebugFunction> logger)
{
    private readonly ILogger<TokenDebugFunction> _logger = logger;

    [Function("DebugToken")]
    public async Task<HttpResponseData> DebugToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "debug/token")] HttpRequestData req)
    {
        try
        {
            // Read the token from the request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            var debugInfo = new
            {
                receivedToken = requestBody?.Trim() ?? "null",
                tokenLength = requestBody?.Trim()?.Length ?? 0,
                timestamp = DateTime.UtcNow,
                headers = req.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };

            // Try to decode if it looks like base64
            string? decodedContent = null;
            string? decodeError = null;
            
            if (!string.IsNullOrEmpty(requestBody?.Trim()))
            {
                try
                {
                    var token = requestBody.Trim();
                    var decodedBytes = Convert.FromBase64String(token);
                    decodedContent = Encoding.UTF8.GetString(decodedBytes);
                }
                catch (Exception ex)
                {
                    decodeError = ex.Message;
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                debug = debugInfo,
                decoded = decodedContent,
                decodeError = decodeError,
                message = "Token debug information"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in token debug");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Debug error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GenerateTestToken")]
    public async Task<HttpResponseData> GenerateTestToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/generate-token")] HttpRequestData req)
    {
        try
        {
            // Generate a test token the same way as the auth function
            var expiryTime = DateTime.UtcNow.AddHours(8);
            var tokenData = $"admin:{expiryTime:yyyy-MM-ddTHH:mm:ssZ}";
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                tokenData = tokenData,
                base64Token = token,
                tokenLength = token.Length,
                expiryTime = expiryTime,
                currentTime = DateTime.UtcNow,
                message = "Test token generated successfully"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating test token");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Generate error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("TestValidation")]
    public async Task<HttpResponseData> TestValidation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "debug/test-validation")] HttpRequestData req)
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var token = requestBody?.Trim() ?? "";
            
            var debugSteps = new List<string>();
            
            debugSteps.Add($"Step 1: Token received: '{token}' (length: {token.Length})");
            
            if (string.IsNullOrEmpty(token))
            {
                debugSteps.Add("Step 2: FAIL - Token is null or empty");
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = false, error = "Missing token", steps = debugSteps });
                return response;
            }
            
            // Base64 decode
            try
            {
                var decodedBytes = Convert.FromBase64String(token);
                var decodedString = Encoding.UTF8.GetString(decodedBytes);
                debugSteps.Add($"Step 2: Base64 decode SUCCESS: '{decodedString}'");
                
                // Check for colon
                var colonIndex = decodedString.IndexOf(':');
                debugSteps.Add($"Step 3: Colon index: {colonIndex}");
                
                if (colonIndex == -1)
                {
                    debugSteps.Add("Step 4: FAIL - No colon found");
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new { success = false, error = "No colon found", steps = debugSteps });
                    return response;
                }
                
                // Split parts
                var parts = new string[]
                {
                    decodedString.Substring(0, colonIndex),
                    decodedString.Substring(colonIndex + 1)
                };
                
                debugSteps.Add($"Step 4: Parts: ['{parts[0]}', '{parts[1]}']");
                debugSteps.Add($"Step 5: Parts.Length == 2: {parts.Length == 2}");
                debugSteps.Add($"Step 6: parts[0] == 'admin': {parts[0] == "admin"}");
                
                if (parts.Length != 2)
                {
                    debugSteps.Add($"Step 7: FAIL - Parts length is {parts.Length}, expected 2");
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new { success = false, error = "Wrong parts length", steps = debugSteps });
                    return response;
                }
                
                if (parts[0] != "admin")
                {
                    debugSteps.Add($"Step 7: FAIL - First part is '{parts[0]}', expected 'admin'");
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new { success = false, error = "Wrong admin part", steps = debugSteps });
                    return response;
                }
                
                debugSteps.Add("Step 7: SUCCESS - Format validation passed");
                
                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new { 
                    success = true, 
                    message = "Token validation successful",
                    decodedString = decodedString,
                    parts = parts,
                    steps = debugSteps 
                });
                return successResponse;
            }
            catch (Exception ex)
            {
                debugSteps.Add($"Step 2: FAIL - Base64 decode error: {ex.Message}");
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { success = false, error = "Base64 decode failed", exception = ex.Message, steps = debugSteps });
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test validation");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Test validation error: {ex.Message}");
            return errorResponse;
        }
    }
}