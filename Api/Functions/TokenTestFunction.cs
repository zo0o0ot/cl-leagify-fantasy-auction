using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Globalization;

namespace LeagifyFantasyAuction.Api.Functions;

/// <summary>
/// Test function to debug token generation and validation.
/// </summary>
public class TokenTestFunction(ILogger<TokenTestFunction> logger)
{
    private readonly ILogger<TokenTestFunction> _logger = logger;

    [Function("TestTokenGeneration")]
    public async Task<HttpResponseData> TestTokenGeneration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test/token")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("=== TOKEN TEST START ===");
            
            // Generate a test token the same way as the auth function
            var expiryTime = DateTime.UtcNow.AddHours(8);
            var tokenData = $"admin:{expiryTime:yyyy-MM-ddTHH:mm:ssZ}";
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
            
            _logger.LogInformation($"Generated token data: {tokenData}");
            _logger.LogInformation($"Generated token: {token}");
            
            // Now test parsing it back
            var decodedBytes = Convert.FromBase64String(token);
            var decodedString = Encoding.UTF8.GetString(decodedBytes);
            var parts = decodedString.Split(':');
            
            _logger.LogInformation($"Decoded string: {decodedString}");
            _logger.LogInformation($"Parts count: {parts.Length}");
            
            if (parts.Length == 2)
            {
                _logger.LogInformation($"Admin part: '{parts[0]}'");
                _logger.LogInformation($"Expiry part: '{parts[1]}'");
                
                // Test exact parsing
                var exactParseSuccess = DateTime.TryParseExact(parts[1], "yyyy-MM-ddTHH:mm:ssZ", 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, 
                    out var exactParsedTime);
                    
                _logger.LogInformation($"Exact parse success: {exactParseSuccess}");
                if (exactParseSuccess)
                {
                    _logger.LogInformation($"Exact parsed time: {exactParsedTime}");
                }
                
                // Test fallback parsing
                var fallbackParseSuccess = DateTime.TryParse(parts[1], null, 
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, 
                    out var fallbackParsedTime);
                    
                _logger.LogInformation($"Fallback parse success: {fallbackParseSuccess}");
                if (fallbackParseSuccess)
                {
                    _logger.LogInformation($"Fallback parsed time: {fallbackParsedTime}");
                }
                
                // Test current time comparison
                _logger.LogInformation($"Current UTC time: {DateTime.UtcNow}");
                _logger.LogInformation($"Original expiry time: {expiryTime}");
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                generatedToken = token,
                tokenData = tokenData,
                decodedString = decodedString,
                parts = parts,
                currentUtc = DateTime.UtcNow,
                expiryTime = expiryTime,
                message = "Check Azure Functions logs for detailed parsing results"
            });
            
            _logger.LogInformation("=== TOKEN TEST END ===");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in token test");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Test error: {ex.Message}");
            return errorResponse;
        }
    }
}