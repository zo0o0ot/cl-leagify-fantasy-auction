using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace LeagifyFantasyAuction.Api.Functions;

public class TestFunction
{
    private readonly ILogger<TestFunction> _logger;

    public TestFunction(ILogger<TestFunction> logger)
    {
        _logger = logger;
    }

    [Function("Test")]
    public async Task<HttpResponseData> Test(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test")] HttpRequestData req)
    {
        _logger.LogInformation("Test function executed successfully");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Hello from Azure Functions!");
        return response;
    }
}