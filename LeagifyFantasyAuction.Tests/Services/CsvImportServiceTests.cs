using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Text;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Tests.Services;

/// <summary>
/// Unit tests for the CsvImportService class.
/// Tests CSV parsing, school data extraction, and logo download integration.
/// </summary>
public class CsvImportServiceTests
{
    private readonly Mock<ILogger<CsvImportService>> _mockLogger;
    private readonly Mock<ISvgDownloadService> _mockSvgDownloadService;
    private readonly CsvImportService _csvImportService;

    public CsvImportServiceTests()
    {
        _mockLogger = new Mock<ILogger<CsvImportService>>();
        _mockSvgDownloadService = new Mock<ISvgDownloadService>();
        _csvImportService = new CsvImportService(_mockLogger.Object, _mockSvgDownloadService.Object);
    }

    [Fact]
    public async Task ImportCsvAsync_WithValidCsvData_ShouldReturnSuccessResult()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,https://example.com/georgia.svg,25.00,Power Conference,5.2,12.3,40.3,33.2
Alabama,SEC,50.0,10,https://example.com/alabama.svg,30.00,Power Conference,9.7,16.8,40.3,33.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(2);
        result.Schools.Should().HaveCount(2);
        result.Errors.Should().BeEmpty();

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.Conference.Should().Be("SEC");
        georgia.ProjectedPoints.Should().Be(45.5m);
        georgia.NumberOfProspects.Should().Be(8);
        georgia.SchoolURL.Should().Be("https://example.com/georgia.svg");
        georgia.SuggestedAuctionValue.Should().Be(25.00m);
        georgia.LeagifyPosition.Should().Be("Power Conference");

        var alabama = result.Schools.First(s => s.Name == "Alabama");
        alabama.Conference.Should().Be("SEC");
        alabama.ProjectedPoints.Should().Be(50.0m);
        alabama.NumberOfProspects.Should().Be(10);
    }

    [Fact]
    public async Task ImportCsvAsync_WithLogoDownload_ShouldCallSvgDownloadService()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,https://example.com/georgia.svg,25.00,Power Conference,5.2,12.3,40.3,33.2";

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
        result.FailedDownloads.Should().BeEmpty();

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.LogoFileName.Should().Be("georgia_logo.svg");

        _mockSvgDownloadService.Verify(
            x => x.DownloadAndStoreSvgAsync("Georgia", "https://example.com/georgia.svg"),
            Times.Once);
    }

    [Fact]
    public async Task ImportCsvAsync_WithFailedLogoDownload_ShouldRecordFailure()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,https://example.com/georgia.svg,25.00,Power Conference,5.2,12.3,40.3,33.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        
        _mockSvgDownloadService
            .Setup(x => x.DownloadAndStoreSvgAsync("Georgia", "https://example.com/georgia.svg"))
            .ReturnsAsync((string?)null); // Simulating download failure

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: true);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue(); // Import should still succeed even if logos fail
        result.SuccessfulDownloads.Should().BeEmpty();
        result.FailedDownloads.Should().HaveCount(1);

        var failedDownload = result.FailedDownloads.First();
        failedDownload.SchoolName.Should().Be("Georgia");
        failedDownload.Url.Should().Be("https://example.com/georgia.svg");
        failedDownload.Error.Should().Be("Download failed");

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.LogoFileName.Should().BeNull();
    }

    [Fact]
    public async Task ImportCsvAsync_WithNoLogoUrls_ShouldNotCallDownloadService()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,,25.00,Power Conference,5.2,12.3,40.3,33.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: true);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.SuccessfulDownloads.Should().BeEmpty();
        result.FailedDownloads.Should().BeEmpty();

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.SchoolURL.Should().BeNullOrEmpty();
        georgia.LogoFileName.Should().BeNull();

        _mockSvgDownloadService.Verify(
            x => x.DownloadAndStoreSvgAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WithDownloadLogosDisabled_ShouldNotCallDownloadService()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,https://example.com/georgia.svg,25.00,Power Conference,5.2,12.3,40.3,33.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.SuccessfulDownloads.Should().BeEmpty();
        result.FailedDownloads.Should().BeEmpty();

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.SchoolURL.Should().Be("https://example.com/georgia.svg");
        georgia.LogoFileName.Should().BeNull();

        _mockSvgDownloadService.Verify(
            x => x.DownloadAndStoreSvgAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportCsvAsync_WithEmptyStream_ShouldReturnEmptyResult()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(0);
        result.Schools.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportCsvAsync_WithOnlyHeaders_ShouldReturnEmptyResult()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(0);
        result.Schools.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportCsvAsync_WithWhitespaceInFields_ShouldTrimValues()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
  Georgia  ,  SEC  ,45.5,8,  https://example.com/georgia.svg  ,25.00,  Power Conference  ,5.2,12.3,40.3,33.2";

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

    [Fact]
    public async Task ImportCsvAsync_WithMissingOptionalFields_ShouldHandleGracefully()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,,25.00,Power Conference,5.2,12.3,40.3,33.2
Alabama,,50.0,10,https://example.com/alabama.svg,,Power Conference,9.7,16.8,40.3,33.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.TotalSchools.Should().Be(2);

        var georgia = result.Schools.First(s => s.Name == "Georgia");
        georgia.SchoolURL.Should().BeNullOrEmpty();
        georgia.SuggestedAuctionValue.Should().Be(25.00m);

        var alabama = result.Schools.First(s => s.Name == "Alabama");
        alabama.Conference.Should().Be("");
        alabama.SuggestedAuctionValue.Should().BeNull();
        alabama.SchoolURL.Should().Be("https://example.com/alabama.svg");
    }

    [Fact]
    public async Task ImportCsvAsync_WithInvalidStream_ShouldReturnFailureResult()
    {
        // Arrange - Create a stream that will cause an exception when read
        var mockStream = new Mock<Stream>();
        mockStream.Setup(x => x.CanRead).Returns(true);
        mockStream.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new InvalidOperationException("Stream read error"));

        // Act
        var result = await _csvImportService.ImportCsvAsync(mockStream.Object, downloadLogos: false);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.TotalSchools.Should().Be(0);
        result.Schools.Should().BeEmpty();
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Contain("CSV import failed");
    }

    [Fact]
    public async Task ImportCsvAsync_WithLogoDownloadException_ShouldContinueProcessing()
    {
        // Arrange
        var csvContent = @"School,Conference,ProjectedPoints,NumberOfProspects,SchoolURL,SuggestedAuctionValue,LeagifyPosition,ProjectedPointsAboveAverage,ProjectedPointsAboveReplacement,AveragePointsForPosition,ReplacementValueAverageForPosition
Georgia,SEC,45.5,8,https://example.com/georgia.svg,25.00,Power Conference,5.2,12.3,40.3,33.2
Alabama,SEC,50.0,10,https://example.com/alabama.svg,30.00,Power Conference,9.7,16.8,40.3,33.2";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        
        _mockSvgDownloadService
            .Setup(x => x.DownloadAndStoreSvgAsync("Georgia", "https://example.com/georgia.svg"))
            .ThrowsAsync(new Exception("Network error"));
        
        _mockSvgDownloadService
            .Setup(x => x.DownloadAndStoreSvgAsync("Alabama", "https://example.com/alabama.svg"))
            .ReturnsAsync("alabama_logo.svg");

        // Act
        var result = await _csvImportService.ImportCsvAsync(stream, downloadLogos: true);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue(); // Overall import should succeed
        result.TotalSchools.Should().Be(2);
        result.Schools.Should().HaveCount(2);
        result.SuccessfulDownloads.Should().Contain("Alabama");
        result.SuccessfulDownloads.Should().NotContain("Georgia");
        result.Errors.Should().HaveCount(1);
        result.Errors.First().Should().Contain("Error processing Georgia");

        // Alabama should still be processed successfully
        var alabama = result.Schools.FirstOrDefault(s => s.Name == "Alabama");
        alabama.Should().NotBeNull();
        alabama!.LogoFileName.Should().Be("alabama_logo.svg");
    }
}