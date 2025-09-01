using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LeagifyFantasyAuction.Api.Data;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Add Entity Framework
        var connectionString = Environment.GetEnvironmentVariable("SQLAZURECONNSTR_DefaultConnection") ?? 
                              Environment.GetEnvironmentVariable("DefaultConnection") ?? 
                              "Server=(localdb)\\mssqllocaldb;Database=LeagifyFantasyAuction;Trusted_Connection=true;MultipleActiveResultSets=true";
        
        services.AddDbContext<LeagifyAuctionDbContext>(options =>
            options.UseSqlServer(connectionString));
    })
    .Build();

host.Run();
