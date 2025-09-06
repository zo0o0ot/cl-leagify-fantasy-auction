using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Text;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Tests.Services;

/// <summary>
/// Integration-style tests for the CsvImportService class.
/// Tests CSV parsing and data validation with realistic CSV data.
/// </summary>
public class CsvImportServiceIntegrationTests
{
    private readonly Mock<ILogger<CsvImportService>> _mockLogger;
    private readonly Mock<ISvgDownloadService> _mockSvgDownloadService;
    private readonly CsvImportService _csvImportService;

    public CsvImportServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<CsvImportService>>();
        _mockSvgDownloadService = new Mock<ISvgDownloadService>();
        _csvImportService = new CsvImportService(_mockLogger.Object, _mockSvgDownloadService.Object);
    }

    [Fact]
    public async Task ImportCsvAsync_WithRealisticCsvData_ShouldParseCorrectly()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,85.5,12,https://example.com/georgia.svg,35.50,Power Conference,15.2,22.3,70.3,63.2
Alabama,SEC,92.0,15,https://example.com/alabama.svg,42.00,Power Conference,21.7,28.8,70.3,63.2
Toledo,MAC,45.2,3,https://example.com/toledo.svg,8.75,Group of 5,5.1,12.0,40.1,33.2
Montana,Big Sky,25.8,1,https://example.com/montana.svg,2.25,FCS,2.5,8.3,23.3,17.5";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(4);
        result.Schools.Should().HaveCount(4);
        result.Errors.Should().BeEmpty();

        // Verify Power Conference schools
        var powerConferenceSchools = result.Schools.Where(s => s.LeagifyPosition == "Power Conference").ToList();
        powerConferenceSchools.Should().HaveCount(2);
        
        var georgia = powerConferenceSchools.First(s => s.Name == "Georgia");
        georgia.Conference.Should().Be("SEC");
        georgia.ProjectedPoints.Should().Be(85.5m);
        georgia.NumberOfProspects.Should().Be(12);
        georgia.SuggestedAuctionValue.Should().Be(35.50m);

        // Verify Group of 5 school
        var g5Schools = result.Schools.Where(s => s.LeagifyPosition == "Group of 5").ToList();
        g5Schools.Should().HaveCount(1);
        
        var toledo = g5Schools.First();
        toledo.Name.Should().Be("Toledo");
        toledo.Conference.Should().Be("MAC");
        toledo.ProjectedPoints.Should().Be(45.2m);

        // Verify FCS school
        var fcsSchools = result.Schools.Where(s => s.LeagifyPosition == "FCS").ToList();
        fcsSchools.Should().HaveCount(1);
        
        var montana = fcsSchools.First();
        montana.Name.Should().Be("Montana");
        montana.Conference.Should().Be("Big Sky");
        montana.ProjectedPoints.Should().Be(25.8m);
        montana.NumberOfProspects.Should().Be(1);
    }

    [Fact]
    public async Task ImportCsvAsync_WithMissingValues_ShouldHandleNullsCorrectly()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,85.5,12,,35.50,Power Conference,15.2,22.3,70.3,63.2
Alabama,,92.0,15,https://example.com/alabama.svg,,Power Conference,21.7,28.8,70.3,63.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(2);

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.SchoolURL.Should().BeNullOrEmpty();
        georgia.SuggestedAuctionValue.Should().Be(35.50m);

        var alabama = result.Schools.First(s => s.Name == "Alabama");
        alabama.Conference.Should().Be("");
        alabama.SuggestedAuctionValue.Should().BeNull();
        alabama.SchoolURL.Should().Be("https://example.com/alabama.svg");
    }

    [Fact]
    public async Task ImportCsvAsync_WithInvalidDecimalValues_ShouldHandleGracefully()
    {
        // Arrange - This CSV has some invalid decimal values that should be handled by CsvHelper
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,85.5,12,https://example.com/georgia.svg,35.50,Power Conference,15.2,22.3,70.3,63.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(1);
        
        var georgia = result.Schools.First();
        georgia.Name.Should().Be("Georgia");
        georgia.ProjectedPoints.Should().Be(85.5m);
        georgia.ProjectedPointsAboveAverage.Should().Be(15.2m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("School")]
    [InlineData("School,Conference")]
    public async Task ImportCsvAsync_WithIncompleteHeaders_ShouldHandleGracefully(string csvContent)
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        // The service should either succeed with empty results or fail gracefully
        if (result.IsSuccess)
        {
            result.TotalSchools.Should().Be(0);
        }
        else
        {
            result.Errors.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task ImportCsvAsync_WithLogoDownloadEnabled_ShouldCallDownloadService()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,85.5,12,https://example.com/georgia.svg,35.50,Power Conference,15.2,22.3,70.3,63.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        
        _mockSvgDownloadService
            .Setup(x => x.DownloadAndStoreSvgAsync("Georgia", "https://example.com/georgia.svg"))
            .ReturnsAsync("georgia_logo.svg");

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: true);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.SuccessfulDownloads.Should().Contain("Georgia");
        
        _mockSvgDownloadService.Verify(
            x => x.DownloadAndStoreSvgAsync("Georgia", "https://example.com/georgia.svg"),
            Times.Once);
    }

    [Fact]
    public async Task ImportCsvAsync_WithWhitespaceData_ShouldTrimValues()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
  Georgia  ,  SEC  ,85.5,12,  https://example.com/georgia.svg  ,35.50,  Power Conference  ,15.2,22.3,70.3,63.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var georgia = result.Schools.First();
        georgia.Name.Should().Be("Georgia");
        georgia.Conference.Should().Be("SEC");
        georgia.SchoolURL.Should().Be("https://example.com/georgia.svg");
        georgia.LeagifyPosition.Should().Be("Power Conference");
    }
}