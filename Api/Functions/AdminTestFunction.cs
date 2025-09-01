using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace LeagifyFantasyAuction.Api.Functions;

public class AdminTestFunction
{
    private readonly ILogger<AdminTestFunction> _logger;

    public AdminTestFunction(ILogger<AdminTestFunction> logger)
    {
        _logger = logger;
    }

    [Function("AdminTest")]
    public async Task<HttpResponseData> AdminTest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/test")] HttpRequestData req)
    {
        _logger.LogInformation("Admin test function executed");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Admin functions are working!");
        return response;
    }
}