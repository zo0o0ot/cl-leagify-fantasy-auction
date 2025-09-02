using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace LeagifyFantasyAuction.Api.Services;

public interface ICsvImportService
{
    Task<CsvImportResult> ImportCsvAsync(Stream csvStream, bool downloadLogos = true);
}

public class CsvImportService : ICsvImportService
{
    private readonly ILogger<CsvImportService> _logger;
    private readonly ISvgDownloadService _svgDownloadService;

    public CsvImportService(ILogger<CsvImportService> logger, ISvgDownloadService svgDownloadService)
    {
        _logger = logger;
        _svgDownloadService = svgDownloadService;
    }

    public async Task<CsvImportResult> ImportCsvAsync(Stream csvStream, bool downloadLogos = true)
    {
        var result = new CsvImportResult();
        var schools = new List<SchoolImportData>();

        try
        {
            // Read CSV data
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            var records = csv.GetRecords<CsvSchoolRecord>().ToList();
            _logger.LogInformation("Found {RecordCount} schools in CSV", records.Count);

            // Process each school
            foreach (var record in records)
            {
                try
                {
                    var schoolData = new SchoolImportData
                    {
                        Name = record.School?.Trim() ?? string.Empty,
                        Conference = record.Conference?.Trim() ?? string.Empty,
                        LeagifyPosition = record.LeagifyPosition?.Trim() ?? string.Empty,
                        ProjectedPoints = record.ProjectedPoints,
                        NumberOfProspects = record.NumberOfProspects,
                        SuggestedAuctionValue = record.SuggestedAuctionValue,
                        ProjectedPointsAboveAverage = record.ProjectedPointsAboveAverage,
                        ProjectedPointsAboveReplacement = record.ProjectedPointsAboveReplacement,
                        AveragePointsForPosition = record.AveragePointsForPosition,
                        ReplacementValueAverageForPosition = record.ReplacementValueAverageForPosition,
                        SchoolURL = record.SchoolURL?.Trim()
                    };

                    // Download SVG if URL is provided and downloadLogos is true
                    if (downloadLogos && !string.IsNullOrEmpty(schoolData.SchoolURL))
                    {
                        _logger.LogInformation("Downloading logo for {SchoolName}", schoolData.Name);
                        var logoFileName = await _svgDownloadService.DownloadAndStoreSvgAsync(
                            schoolData.Name, schoolData.SchoolURL);
                        
                        if (!string.IsNullOrEmpty(logoFileName))
                        {
                            schoolData.LogoFileName = logoFileName;
                            result.SuccessfulDownloads.Add(schoolData.Name);
                            _logger.LogInformation("Successfully downloaded logo for {SchoolName}: {FileName}", 
                                schoolData.Name, logoFileName);
                        }
                        else
                        {
                            result.FailedDownloads.Add(new FailedDownload 
                            { 
                                SchoolName = schoolData.Name, 
                                Url = schoolData.SchoolURL,
                                Error = "Download failed"
                            });
                            _logger.LogWarning("Failed to download logo for {SchoolName} from {Url}", 
                                schoolData.Name, schoolData.SchoolURL);
                        }
                    }

                    schools.Add(schoolData);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing school record: {SchoolName}", record.School);
                    result.Errors.Add($"Error processing {record.School}: {ex.Message}");
                }
            }

            result.Schools = schools;
            result.IsSuccess = true;
            result.TotalSchools = schools.Count;

            _logger.LogInformation("CSV import completed. Total: {Total}, Successful downloads: {Success}, Failed downloads: {Failed}", 
                result.TotalSchools, result.SuccessfulDownloads.Count, result.FailedDownloads.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing CSV");
            result.IsSuccess = false;
            result.Errors.Add($"CSV import failed: {ex.Message}");
        }

        return result;
    }
}

public class CsvSchoolRecord
{
    public string School { get; set; } = string.Empty;
    public string Conference { get; set; } = string.Empty;
    public decimal ProjectedPoints { get; set; }
    public int NumberOfProspects { get; set; }
    public string? SchoolURL { get; set; }
    public decimal? SuggestedAuctionValue { get; set; }
    public string LeagifyPosition { get; set; } = string.Empty;
    public decimal ProjectedPointsAboveAverage { get; set; }
    public decimal ProjectedPointsAboveReplacement { get; set; }
    public decimal AveragePointsForPosition { get; set; }
    public decimal ReplacementValueAverageForPosition { get; set; }
}

public class SchoolImportData
{
    public string Name { get; set; } = string.Empty;
    public string Conference { get; set; } = string.Empty;
    public string LeagifyPosition { get; set; } = string.Empty;
    public decimal ProjectedPoints { get; set; }
    public int NumberOfProspects { get; set; }
    public decimal? SuggestedAuctionValue { get; set; }
    public decimal ProjectedPointsAboveAverage { get; set; }
    public decimal ProjectedPointsAboveReplacement { get; set; }
    public decimal AveragePointsForPosition { get; set; }
    public decimal ReplacementValueAverageForPosition { get; set; }
    public string? SchoolURL { get; set; }
    public string? LogoFileName { get; set; }
}

public class CsvImportResult
{
    public bool IsSuccess { get; set; }
    public List<SchoolImportData> Schools { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> SuccessfulDownloads { get; set; } = new();
    public List<FailedDownload> FailedDownloads { get; set; } = new();
    public int TotalSchools { get; set; }
}

public class FailedDownload
{
    public string SchoolName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}