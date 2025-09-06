using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Net;
using System.Text;
using LeagifyFantasyAuction.Api.Services;
using Moq.Protected;

namespace LeagifyFantasyAuction.Tests.Services;

/// <summary>
/// Unit tests for the SvgDownloadService class.
/// Tests SVG downloading, file sanitization, validation, and error handling.
/// </summary>
public class SvgDownloadServiceTests : IDisposable
{
    private readonly Mock<ILogger<SvgDownloadService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly SvgDownloadService _svgDownloadService;
    private readonly string _testDirectory;

    public SvgDownloadServiceTests()
    {
        _mockLogger = new Mock<ILogger<SvgDownloadService>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _svgDownloadService = new SvgDownloadService(_mockLogger.Object, _httpClient);
        
        // Create a temporary test directory for file operations
        _testDirectory = Path.Combine(Path.GetTempPath(), "SvgDownloadServiceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithValidSvgContent_ShouldReturnFileName()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        var svgUrl = "https://example.com/georgia-tech.svg";
        var validSvgContent = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"">
                                  <circle cx=""50"" cy=""50"" r=""40"" fill=""blue"" />
                                </svg>";

        SetupHttpResponse(HttpStatusCode.OK, validSvgContent);

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("GeorgiaTech.svg"); // Sanitized filename
        
        VerifyHttpRequest(svgUrl);
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithEmptyUrl_ShouldReturnNull()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        var svgUrl = "";

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().BeNull();
        
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Empty SVG URL provided")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithNullUrl_ShouldReturnNull()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        string? svgUrl = null;

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithHttpError_ShouldReturnNull()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        var svgUrl = "https://example.com/georgia-tech.svg";

        SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().BeNull();
        
        VerifyHttpRequest(svgUrl);
        
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to download SVG")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithInvalidSvgContent_ShouldReturnNull()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        var svgUrl = "https://example.com/georgia-tech.svg";
        var invalidContent = "<html><body>This is not SVG</body></html>";

        SetupHttpResponse(HttpStatusCode.OK, invalidContent);

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().BeNull();
        
        VerifyHttpRequest(svgUrl);
        
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not valid SVG")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithHttpException_ShouldReturnNull()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        var svgUrl = "https://example.com/georgia-tech.svg";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().BeNull();
        
        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP error downloading SVG")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithTimeout_ShouldReturnNull()
    {
        // Arrange
        var schoolName = "Georgia Tech";
        var svgUrl = "https://example.com/georgia-tech.svg";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().BeNull();
        
        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Request timeout downloading SVG")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Georgia Tech", "GeorgiaTech.svg")]
    [InlineData("University of Alabama", "UniversityofAlabama.svg")]
    [InlineData("Texas A&M", "TexasAM.svg")]
    [InlineData("Miami (FL)", "MiamiFL.svg")]
    [InlineData("St. John's", "StJohns.svg")]
    [InlineData("USC", "USC.svg")]
    public void GetLocalSvgPath_WithVariousSchoolNames_ShouldReturnSanitizedPath(string schoolName, string expectedFileName)
    {
        // Act
        var result = _svgDownloadService.GetLocalSvgPath(schoolName);

        // Assert
        result.Should().EndWith(expectedFileName);
        result.Should().Contain("wwwroot");
        result.Should().Contain("images");
    }

    [Fact]
    public async Task DownloadAndStoreSvgAsync_WithSpecialCharactersInSchoolName_ShouldSanitizeFileName()
    {
        // Arrange
        var schoolName = "Miami (FL)";
        var svgUrl = "https://example.com/miami-fl.svg";
        var validSvgContent = @"<svg xmlns=""http://www.w3.org/2000/svg""><rect width=""10"" height=""10""/></svg>";

        SetupHttpResponse(HttpStatusCode.OK, validSvgContent);

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().Be("MiamiFL.svg");
    }

    [Theory]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><rect width=""10"" height=""10""/></svg>")]
    [InlineData(@"<?xml version=""1.0""?><svg xmlns=""http://www.w3.org/2000/svg""><circle r=""5""/></svg>")]
    [InlineData(@"<svg viewBox=""0 0 100 100""><path d=""M10 10L90 90""/></svg>")]
    public async Task DownloadAndStoreSvgAsync_WithVariousValidSvgFormats_ShouldSucceed(string svgContent)
    {
        // Arrange
        var schoolName = "Test School";
        var svgUrl = "https://example.com/test.svg";

        SetupHttpResponse(HttpStatusCode.OK, svgContent);

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().Be("TestSchool.svg");
    }

    [Theory]
    [InlineData("<html><body>Not SVG</body></html>")]
    [InlineData("This is just plain text")]
    [InlineData("")]
    [InlineData("<xml><notsvg/></xml>")]
    public async Task DownloadAndStoreSvgAsync_WithInvalidSvgFormats_ShouldReturnNull(string invalidContent)
    {
        // Arrange
        var schoolName = "Test School";
        var svgUrl = "https://example.com/test.svg";

        SetupHttpResponse(HttpStatusCode.OK, invalidContent);

        // Act
        var result = await _svgDownloadService.DownloadAndStoreSvgAsync(schoolName, svgUrl);

        // Assert
        result.Should().BeNull();
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8)
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void VerifyHttpRequest(string expectedUrl)
    {
        _mockHttpMessageHandler
            .Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString() == expectedUrl),
                ItExpr.IsAny<CancellationToken>());
    }
}