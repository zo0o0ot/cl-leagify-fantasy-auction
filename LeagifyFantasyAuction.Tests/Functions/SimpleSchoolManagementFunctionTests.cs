using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using FluentAssertions;
using System.Net;
using System.Text;
using System.Text.Json;
using LeagifyFantasyAuction.Api.Functions;
using LeagifyFantasyAuction.Api.Services;

namespace LeagifyFantasyAuction.Tests.Functions;

/// <summary>
/// Unit tests for the SimpleSchoolManagementFunction class.
/// Tests all CRUD operations for school management API endpoints.
/// </summary>
public class SimpleSchoolManagementFunctionTests
{
    private readonly Mock<ILogger<SimpleSchoolManagementFunction>> _mockLogger;
    private readonly Mock<ICsvImportService> _mockCsvImportService;
    private readonly SimpleSchoolManagementFunction _function;
    private readonly Mock<HttpRequestData> _mockRequest;
    private readonly Mock<FunctionContext> _mockContext;

    public SimpleSchoolManagementFunctionTests()
    {
        _mockLogger = new Mock<ILogger<SimpleSchoolManagementFunction>>();
        _mockCsvImportService = new Mock<ICsvImportService>();
        _function = new SimpleSchoolManagementFunction(_mockLogger.Object, _mockCsvImportService.Object);
        _mockRequest = new Mock<HttpRequestData>(Mock.Of<FunctionContext>());
        _mockContext = new Mock<FunctionContext>();
        
        SetupValidAuthToken();
    }

    [Fact]
    public async Task GetSchools_WithValidAuth_ShouldReturnSchoolsList()
    {
        // Arrange
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        // Act
        var result = await _function.GetSchools(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSchools_WithInvalidAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        SetupInvalidAuthToken();
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.Unauthorized)).Returns(mockResponse.Object);

        // Act
        var result = await _function.GetSchools(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateSchool_WithValidData_ShouldReturnCreatedSchool()
    {
        // Arrange
        var schoolDto = new { Name = "Georgia Tech", LogoURL = "https://example.com/gt.svg" };
        var jsonContent = JsonSerializer.Serialize(schoolDto);
        
        SetupRequestBody(jsonContent);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        var mockHeaders = new Mock<HttpHeadersCollection>();
        mockResponse.Setup(r => r.Headers).Returns(mockHeaders.Object);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.Created)).Returns(mockResponse.Object);

        // Act
        var result = await _function.CreateSchool(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.Created);
        mockHeaders.Verify(h => h.Add("Location", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateSchool_WithEmptyName_ShouldReturnBadRequest()
    {
        // Arrange
        var schoolDto = new { Name = "", LogoURL = "https://example.com/test.svg" };
        var jsonContent = JsonSerializer.Serialize(schoolDto);
        
        SetupRequestBody(jsonContent);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.BadRequest)).Returns(mockResponse.Object);

        // Act
        var result = await _function.CreateSchool(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchool_WithInvalidJson_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        SetupRequestBody(invalidJson);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.BadRequest)).Returns(mockResponse.Object);

        // Act
        var result = await _function.CreateSchool(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchool_WithEmptyBody_ShouldReturnBadRequest()
    {
        // Arrange
        SetupRequestBody("");
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.BadRequest)).Returns(mockResponse.Object);

        // Act
        var result = await _function.CreateSchool(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSchool_WithInvalidAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        SetupInvalidAuthToken();
        var schoolDto = new { Name = "Georgia Tech" };
        var jsonContent = JsonSerializer.Serialize(schoolDto);
        SetupRequestBody(jsonContent);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.Unauthorized)).Returns(mockResponse.Object);

        // Act
        var result = await _function.CreateSchool(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportSchoolsCsv_WithValidCsv_ShouldReturnSuccess()
    {
        // Arrange
        var csvContent = "School,Conference\nGeorgia,SEC\nAlabama,SEC";
        SetupRequestBody(csvContent);
        
        var importResult = new CsvImportResult
        {
            IsSuccess = true,
            TotalSchools = 2,
            Schools = new List<SchoolImportData>
            {
                new() { Name = "Georgia", Conference = "SEC" },
                new() { Name = "Alabama", Conference = "SEC" }
            }
        };
        
        _mockCsvImportService
            .Setup(s => s.ImportCsvAsync(It.IsAny<Stream>(), true))
            .ReturnsAsync(importResult);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        // Act
        var result = await _function.ImportSchoolsCsv(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.OK);
        _mockCsvImportService.Verify(s => s.ImportCsvAsync(It.IsAny<Stream>(), true), Times.Once);
    }

    [Fact]
    public async Task ImportSchoolsCsv_WithImportFailure_ShouldReturnBadRequest()
    {
        // Arrange
        var csvContent = "invalid,csv,format";
        SetupRequestBody(csvContent);
        
        var importResult = new CsvImportResult
        {
            IsSuccess = false,
            Errors = new List<string> { "Invalid CSV format" }
        };
        
        _mockCsvImportService
            .Setup(s => s.ImportCsvAsync(It.IsAny<Stream>(), true))
            .ReturnsAsync(importResult);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.BadRequest)).Returns(mockResponse.Object);

        // Act
        var result = await _function.ImportSchoolsCsv(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportSchoolsCsv_WithInvalidAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        SetupInvalidAuthToken();
        SetupRequestBody("School,Conference\nTest,Test");
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.Unauthorized)).Returns(mockResponse.Object);

        // Act
        var result = await _function.ImportSchoolsCsv(_mockRequest.Object);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.Unauthorized);
        _mockCsvImportService.Verify(s => s.ImportCsvAsync(It.IsAny<Stream>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSchool_WithValidData_ShouldReturnUpdatedSchool()
    {
        // Arrange
        var schoolId = 1;
        var updateDto = new { Name = "Updated Georgia Tech", LogoURL = "https://example.com/updated.svg" };
        var jsonContent = JsonSerializer.Serialize(updateDto);
        
        // First create a school to update
        await CreateTestSchool("Georgia Tech");
        
        SetupRequestBody(jsonContent);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        // Act
        var result = await _function.UpdateSchool(_mockRequest.Object, schoolId);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateSchool_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var schoolId = 999;
        var updateDto = new { Name = "Non-existent School" };
        var jsonContent = JsonSerializer.Serialize(updateDto);
        
        SetupRequestBody(jsonContent);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.NotFound)).Returns(mockResponse.Object);

        // Act
        var result = await _function.UpdateSchool(_mockRequest.Object, schoolId);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSchool_WithExistingId_ShouldReturnSuccess()
    {
        // Arrange
        var schoolId = 1;
        
        // First create a school to delete
        await CreateTestSchool("Test School");
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.OK)).Returns(mockResponse.Object);

        // Act
        var result = await _function.DeleteSchool(_mockRequest.Object, schoolId);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteSchool_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var schoolId = 999;
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.NotFound)).Returns(mockResponse.Object);

        // Act
        var result = await _function.DeleteSchool(_mockRequest.Object, schoolId);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSchool_WithInvalidAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        SetupInvalidAuthToken();
        var schoolId = 1;
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.Unauthorized)).Returns(mockResponse.Object);

        // Act
        var result = await _function.DeleteSchool(_mockRequest.Object, schoolId);

        // Assert
        result.Should().NotBeNull();
        mockResponse.VerifySet(r => r.StatusCode = HttpStatusCode.Unauthorized);
    }

    private void SetupValidAuthToken()
    {
        var authHeader = "Bearer valid_token";
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            { "Authorization", new[] { authHeader } }
        };
        
        _mockRequest.Setup(r => r.Headers).Returns(new HttpHeadersCollection(headers));
        
        // Mock the static method call - this is complex with Moq, so we'll assume valid auth for most tests
        // In a real implementation, you might want to use dependency injection for the auth validation
    }

    private void SetupInvalidAuthToken()
    {
        var headers = new Dictionary<string, IEnumerable<string>>();
        _mockRequest.Setup(r => r.Headers).Returns(new HttpHeadersCollection(headers));
    }

    private void SetupRequestBody(string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        _mockRequest.Setup(r => r.Body).Returns(stream);
    }

    private async Task CreateTestSchool(string schoolName)
    {
        // Helper method to create a test school for update/delete tests
        var schoolDto = new { Name = schoolName, LogoURL = "https://example.com/test.svg" };
        var jsonContent = JsonSerializer.Serialize(schoolDto);
        
        SetupRequestBody(jsonContent);
        
        var mockResponse = new Mock<HttpResponseData>(_mockContext.Object);
        mockResponse.SetupProperty(r => r.StatusCode);
        var mockHeaders = new Mock<HttpHeadersCollection>();
        mockResponse.Setup(r => r.Headers).Returns(mockHeaders.Object);
        _mockRequest.Setup(r => r.CreateResponse(HttpStatusCode.Created)).Returns(mockResponse.Object);
        
        await _function.CreateSchool(_mockRequest.Object);
        
        // Reset the request body for the actual test
        SetupRequestBody("");
    }
}