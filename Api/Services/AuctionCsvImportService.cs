using Microsoft.Extensions.Logging;
using LeagifyFantasyAuction.Api.Models;
using LeagifyFantasyAuction.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeagifyFantasyAuction.Api.Services;

/// <summary>
/// Service for importing CSV data specifically for auction creation.
/// Handles school matching, validation, and AuctionSchool entity creation.
/// </summary>
public interface IAuctionCsvImportService
{
    /// <summary>
    /// Parses CSV data and returns a preview with school matching information.
    /// Does not persist data - allows for confirmation before final import.
    /// </summary>
    Task<AuctionCsvPreviewResult> PreviewCsvImportAsync(Stream csvStream, int auctionId);
    
    /// <summary>
    /// Completes the CSV import after user confirmation of school matches.
    /// Creates AuctionSchool entities for the specified auction.
    /// </summary>
    Task<AuctionCsvImportResult> CompleteCsvImportAsync(int auctionId, List<ConfirmedSchoolMatch> confirmedMatches);
}

/// <summary>
/// Implementation of auction-specific CSV import functionality.
/// </summary>
public class AuctionCsvImportService(ILogger<AuctionCsvImportService> logger, 
    LeagifyAuctionDbContext dbContext, 
    ICsvImportService baseCsvService) : IAuctionCsvImportService
{
    private readonly ILogger<AuctionCsvImportService> _logger = logger;
    private readonly LeagifyAuctionDbContext _dbContext = dbContext;
    private readonly ICsvImportService _baseCsvService = baseCsvService;

    /// <summary>
    /// Parses CSV and provides preview with school matching options.
    /// </summary>
    public async Task<AuctionCsvPreviewResult> PreviewCsvImportAsync(Stream csvStream, int auctionId)
    {
        var result = new AuctionCsvPreviewResult { AuctionId = auctionId };

        try
        {
            _logger.LogInformation("Starting CSV preview for auction {AuctionId}", auctionId);

            // First, parse the CSV using the existing service (without logo downloads for preview)
            var csvResult = await _baseCsvService.ImportCsvAsync(csvStream, downloadLogos: false);
            
            if (!csvResult.IsSuccess)
            {
                result.IsSuccess = false;
                result.Errors = csvResult.Errors;
                return result;
            }

            // Get all existing schools for matching
            var existingSchools = await _dbContext.Schools.ToListAsync();
            
            // Process each CSV school and find matches
            var schoolMatches = new List<SchoolMatchPreview>();
            int importOrder = 1;

            foreach (var csvSchool in csvResult.Schools)
            {
                var matchPreview = FindSchoolMatch(csvSchool, existingSchools, importOrder++);
                schoolMatches.Add(matchPreview);
            }

            result.SchoolMatches = schoolMatches;
            result.IsSuccess = true;
            result.TotalSchools = schoolMatches.Count;
            result.ExactMatches = schoolMatches.Count(m => m.MatchType == SchoolMatchType.Exact);
            result.FuzzyMatches = schoolMatches.Count(m => m.MatchType == SchoolMatchType.Fuzzy);
            result.NoMatches = schoolMatches.Count(m => m.MatchType == SchoolMatchType.NoMatch);

            _logger.LogInformation("CSV preview completed. Total: {Total}, Exact: {Exact}, Fuzzy: {Fuzzy}, No Match: {NoMatch}",
                result.TotalSchools, result.ExactMatches, result.FuzzyMatches, result.NoMatches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV preview for auction {AuctionId}", auctionId);
            result.IsSuccess = false;
            result.Errors.Add($"Preview failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Completes the import after user confirmation of matches.
    /// </summary>
    public async Task<AuctionCsvImportResult> CompleteCsvImportAsync(int auctionId, List<ConfirmedSchoolMatch> confirmedMatches)
    {
        var result = new AuctionCsvImportResult { AuctionId = auctionId };

        try
        {
            _logger.LogInformation("Starting confirmed CSV import for auction {AuctionId} with {MatchCount} schools", 
                auctionId, confirmedMatches.Count);

            // Verify auction exists
            var auction = await _dbContext.Auctions.FindAsync(auctionId);
            if (auction == null)
            {
                result.IsSuccess = false;
                result.Errors.Add($"Auction {auctionId} not found");
                return result;
            }

            // Clear existing AuctionSchool records for this auction
            var existingAuctionSchools = _dbContext.AuctionSchools.Where(a => a.AuctionId == auctionId);
            _dbContext.AuctionSchools.RemoveRange(existingAuctionSchools);

            // Create AuctionSchool entities from confirmed matches
            var auctionSchools = new List<AuctionSchool>();
            
            foreach (var match in confirmedMatches)
            {
                var auctionSchool = new AuctionSchool
                {
                    AuctionId = auctionId,
                    SchoolId = match.SchoolId,
                    Conference = match.CsvData.Conference,
                    LeagifyPosition = match.CsvData.LeagifyPosition,
                    ProjectedPoints = match.CsvData.ProjectedPoints,
                    NumberOfProspects = match.CsvData.NumberOfProspects,
                    SuggestedAuctionValue = match.CsvData.SuggestedAuctionValue,
                    ProjectedPointsAboveAverage = match.CsvData.ProjectedPointsAboveAverage,
                    ProjectedPointsAboveReplacement = match.CsvData.ProjectedPointsAboveReplacement,
                    AveragePointsForPosition = match.CsvData.AveragePointsForPosition,
                    ReplacementValueAverageForPosition = match.CsvData.ReplacementValueAverageForPosition,
                    ImportOrder = match.ImportOrder,
                    IsAvailable = true
                };

                auctionSchools.Add(auctionSchool);
            }

            // Save to database
            _dbContext.AuctionSchools.AddRange(auctionSchools);
            await _dbContext.SaveChangesAsync();

            result.IsSuccess = true;
            result.TotalSchools = auctionSchools.Count;

            _logger.LogInformation("CSV import completed for auction {AuctionId}. Imported {Count} schools", 
                auctionId, result.TotalSchools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during confirmed CSV import for auction {AuctionId}", auctionId);
            result.IsSuccess = false;
            result.Errors.Add($"Import failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Finds the best matching school for a CSV record.
    /// </summary>
    private SchoolMatchPreview FindSchoolMatch(SchoolImportData csvSchool, List<School> existingSchools, int importOrder)
    {
        var match = new SchoolMatchPreview
        {
            CsvData = csvSchool,
            ImportOrder = importOrder
        };

        // Try exact match first
        var exactMatch = existingSchools.FirstOrDefault(s => 
            string.Equals(s.Name, csvSchool.Name, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            match.MatchType = SchoolMatchType.Exact;
            match.MatchedSchool = exactMatch;
            match.Confidence = 1.0;
            return match;
        }

        // Try fuzzy matching
        var fuzzyMatches = existingSchools
            .Select(school => new
            {
                School = school,
                Similarity = CalculateStringSimilarity(csvSchool.Name, school.Name)
            })
            .Where(x => x.Similarity > 0.7) // 70% similarity threshold
            .OrderByDescending(x => x.Similarity)
            .ToList();

        if (fuzzyMatches.Any())
        {
            var bestMatch = fuzzyMatches.First();
            match.MatchType = SchoolMatchType.Fuzzy;
            match.MatchedSchool = bestMatch.School;
            match.Confidence = bestMatch.Similarity;
            
            // Provide alternative matches for user consideration
            match.AlternativeMatches = fuzzyMatches.Skip(1).Take(3)
                .Select(x => x.School).ToList();
        }
        else
        {
            match.MatchType = SchoolMatchType.NoMatch;
            match.Confidence = 0.0;
        }

        return match;
    }

    /// <summary>
    /// Calculates string similarity using Levenshtein distance.
    /// </summary>
    private double CalculateStringSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0.0;

        var distance = LevenshteinDistance(source.ToLower(), target.ToLower());
        var maxLength = Math.Max(source.Length, target.Length);
        return 1.0 - (double)distance / maxLength;
    }

    /// <summary>
    /// Calculates Levenshtein distance between two strings.
    /// </summary>
    private int LevenshteinDistance(string source, string target)
    {
        var sourceLength = source.Length;
        var targetLength = target.Length;
        var matrix = new int[sourceLength + 1, targetLength + 1];

        for (var i = 0; i <= sourceLength; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= targetLength; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[sourceLength, targetLength];
    }
}

/// <summary>
/// Result of CSV preview operation.
/// </summary>
public class AuctionCsvPreviewResult
{
    public int AuctionId { get; set; }
    public bool IsSuccess { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<SchoolMatchPreview> SchoolMatches { get; set; } = new();
    public int TotalSchools { get; set; }
    public int ExactMatches { get; set; }
    public int FuzzyMatches { get; set; }
    public int NoMatches { get; set; }
}

/// <summary>
/// Result of completed CSV import.
/// </summary>
public class AuctionCsvImportResult
{
    public int AuctionId { get; set; }
    public bool IsSuccess { get; set; }
    public List<string> Errors { get; set; } = new();
    public int TotalSchools { get; set; }
}

/// <summary>
/// Preview of how a CSV school matches with existing schools.
/// </summary>
public class SchoolMatchPreview
{
    public SchoolImportData CsvData { get; set; } = new();
    public int ImportOrder { get; set; }
    public SchoolMatchType MatchType { get; set; }
    public School? MatchedSchool { get; set; }
    public double Confidence { get; set; }
    public List<School> AlternativeMatches { get; set; } = new();
}

/// <summary>
/// User-confirmed school match for final import.
/// </summary>
public class ConfirmedSchoolMatch
{
    public int SchoolId { get; set; }
    public SchoolImportData CsvData { get; set; } = new();
    public int ImportOrder { get; set; }
}

/// <summary>
/// Types of school matching results.
/// </summary>
public enum SchoolMatchType
{
    Exact,      // Perfect name match
    Fuzzy,      // Similar name match
    NoMatch     // No suitable match found
}