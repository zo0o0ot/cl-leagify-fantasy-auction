using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LeagifyFantasyAuction.Api.Data;
using LeagifyFantasyAuction.Api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Add Entity Framework
        var connectionString = Environment.GetEnvironmentVariable("SQLAZURECONNSTR_DefaultConnection") ?? 
                              Environment.GetEnvironmentVariable("DefaultConnection") ?? 
                              "Server=(localdb)\\mssqllocaldb;Database=LeagifyFantasyAuction;Trusted_Connection=true;MultipleActiveResultSets=true";
        
        services.AddDbContext<LeagifyAuctionDbContext>(options =>
        {
            if (connectionString.Contains("Data Source") && connectionString.Contains(".db"))
            {
                // SQLite for local development
                options.UseSqlite(connectionString);
            }
            else
            {
                // SQL Server for Azure
                options.UseSqlServer(connectionString);
            }
        });

        // Add HttpClient for SVG downloads
        services.AddHttpClient<ISvgDownloadService, SvgDownloadService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "LeagifyFantasyAuction/1.0");
        });

        // Add services
        services.AddScoped<ISvgDownloadService, SvgDownloadService>();
        services.AddScoped<ICsvImportService, CsvImportService>();
        services.AddScoped<IAuctionService, AuctionService>();
    })
    .Build();

// Initialize database on startup
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LeagifyAuctionDbContext>();
    try
    {
        // Create database and tables if they don't exist
        dbContext.Database.EnsureCreated();
        Console.WriteLine("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
        // Continue running even if database init fails - for Azure deployment scenarios
    }
}

host.Run();
