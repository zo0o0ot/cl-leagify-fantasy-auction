using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace LeagifyFantasyAuction.Api.Services;

public interface ISvgDownloadService
{
    Task<string?> DownloadAndStoreSvgAsync(string schoolName, string svgUrl);
    Task<bool> DeleteSvgAsync(string schoolName);
    string GetLocalSvgPath(string schoolName);
}

public class SvgDownloadService : ISvgDownloadService
{
    private readonly ILogger<SvgDownloadService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _svgStoragePath;

    public SvgDownloadService(ILogger<SvgDownloadService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        
        // Set storage path but don't create directory until needed (Azure Functions have read-only file system)
        _svgStoragePath = Path.Combine("wwwroot", "images");
    }

    public async Task<string?> DownloadAndStoreSvgAsync(string schoolName, string svgUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(svgUrl))
            {
                _logger.LogWarning("Empty SVG URL provided for school: {SchoolName}", schoolName);
                return null;
            }

            // Create directory only when actually needed
            try
            {
                Directory.CreateDirectory(_svgStoragePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot create directory {Path} - file system may be read-only (Azure Functions). SVG download skipped.", _svgStoragePath);
                return null;
            }

            // Sanitize school name for filename
            var sanitizedName = SanitizeFileName(schoolName);
            var fileName = $"{sanitizedName}.svg";
            var filePath = Path.Combine(_svgStoragePath, fileName);

            _logger.LogInformation("Downloading SVG for {SchoolName} from {SvgUrl}", schoolName, svgUrl);

            // Download the SVG content
            var response = await _httpClient.GetAsync(svgUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download SVG for {SchoolName}. Status: {StatusCode}", 
                    schoolName, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            
            // Validate that the content is actually SVG
            if (!IsValidSvg(content))
            {
                _logger.LogWarning("Downloaded content for {SchoolName} is not valid SVG", schoolName);
                return null;
            }

            // Save to file
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            
            _logger.LogInformation("Successfully saved SVG for {SchoolName} as {FileName}", schoolName, fileName);
            
            return fileName;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading SVG for {SchoolName} from {SvgUrl}", schoolName, svgUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout downloading SVG for {SchoolName} from {SvgUrl}", schoolName, svgUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading SVG for {SchoolName} from {SvgUrl}", schoolName, svgUrl);
            return null;
        }
    }

    public Task<bool> DeleteSvgAsync(string schoolName)
    {
        try
        {
            var sanitizedName = SanitizeFileName(schoolName);
            var fileName = $"{sanitizedName}.svg";
            var filePath = Path.Combine(_svgStoragePath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted SVG file for {SchoolName}: {FileName}", schoolName, fileName);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SVG file for {SchoolName}", schoolName);
            return Task.FromResult(false);
        }
    }

    public string GetLocalSvgPath(string schoolName)
    {
        var sanitizedName = SanitizeFileName(schoolName);
        var fileName = $"{sanitizedName}.svg";
        return Path.Combine(_svgStoragePath, fileName);
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid characters and replace with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        
        // Replace multiple underscores with single underscore
        sanitized = Regex.Replace(sanitized, "_+", "_");
        
        // Remove leading/trailing underscores
        sanitized = sanitized.Trim('_');
        
        // Ensure it's not empty
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "unknown_school";
        }

        return sanitized;
    }

    private static bool IsValidSvg(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Basic SVG validation - check for SVG tag
        return content.TrimStart().StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }
}