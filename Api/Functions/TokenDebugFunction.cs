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
}