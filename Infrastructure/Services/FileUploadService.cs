using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ShreeJewellers.Infrastructure.Services;

public interface IFileUploadService
{
    Task<string> UploadAsync(IFormFile file, string category, string userId);
    Task<string> UploadBase64Async(string base64Data, string extension, string category, string userId);
    Task DeleteAsync(string fileUrl);
    bool IsValidFile(IFormFile file, out string errorMessage);
}

/// <summary>
/// Secure file upload handler.
/// Files are stored OUTSIDE wwwroot under /secure-uploads/{category}/{userId}/{guid}.ext
/// Access is controlled via an authenticated download endpoint — never via static file serving.
/// </summary>
public class FileUploadService : IFileUploadService
{
    private readonly string _basePath;
    private readonly long _maxFileSizeBytes;
    private readonly ILogger<FileUploadService> _logger;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

    private static readonly HashSet<string> AllowedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "application/pdf"
        };

    // Magic bytes for file type validation (prevents MIME spoofing)
    private static readonly Dictionary<string, byte[]> MagicBytes = new()
    {
        { ".jpg",  new byte[] { 0xFF, 0xD8, 0xFF } },
        { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { ".png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { ".pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46 } }     // %PDF
    };

    public FileUploadService(IConfiguration config, ILogger<FileUploadService> logger)
    {
        _logger = logger;
        var configuredPath = config["FileStorage:BasePath"];
        if (string.IsNullOrWhiteSpace(configuredPath) || (!OperatingSystem.IsWindows() && configuredPath.Contains(':')))
        {
            _basePath = Path.Combine(Directory.GetCurrentDirectory(), "secure-uploads");
        }
        else
        {
            _basePath = configuredPath;
        }

        var maxMb = double.Parse(config["FileStorage:MaxFileSizeMB"] ?? "5");
        _maxFileSizeBytes = (long)(maxMb * 1024 * 1024);

        try
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create file storage directory at {Path}. Falling back to temp directory.", _basePath);
            _basePath = Path.Combine(Path.GetTempPath(), "secure-uploads");
            Directory.CreateDirectory(_basePath);
        }
    }

    // ── Upload IFormFile ──────────────────────────────────────────────────

    public async Task<string> UploadAsync(IFormFile file, string category, string userId)
    {
        if (!IsValidFile(file, out var error))
            throw new InvalidOperationException(error);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativePath = BuildRelativePath(category, userId, ext);
        var fullPath = Path.Combine(_basePath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream);

        _logger.LogInformation("File uploaded: {Category}/{UserId}/{File}",
            category, MaskUserId(userId), Path.GetFileName(fullPath));

        // Return relative path used as the stored URL
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    // ── Upload Base64 (from mobile/API clients) ───────────────────────────

    public async Task<string> UploadBase64Async(
        string base64Data, string extension, string category, string userId)
    {
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant()
                                            : $".{extension.ToLowerInvariant()}";

        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException($"File type '{ext}' is not allowed.");

        byte[] fileBytes;
        try
        {
            // Strip data URL prefix if present: "data:image/jpeg;base64,..."
            var data = base64Data.Contains(',')
                ? base64Data.Split(',')[1]
                : base64Data;
            fileBytes = Convert.FromBase64String(data);
        }
        catch
        {
            throw new InvalidOperationException("Invalid Base64 file data.");
        }

        if (fileBytes.Length > _maxFileSizeBytes)
            throw new InvalidOperationException($"File exceeds maximum size of {_maxFileSizeBytes / 1024 / 1024}MB.");

        ValidateMagicBytes(fileBytes, ext);

        var relativePath = BuildRelativePath(category, userId, ext);
        var fullPath = Path.Combine(_basePath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, fileBytes);

        _logger.LogInformation("Base64 file uploaded: {Category}/{UserId}",
            category, MaskUserId(userId));

        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    // ── Delete ────────────────────────────────────────────────────────────

    public Task DeleteAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return Task.CompletedTask;

        var fullPath = Path.Combine(_basePath, fileUrl.Replace('/', Path.DirectorySeparatorChar));

        // Security: ensure path stays within base directory (path traversal prevention)
        var resolvedPath = Path.GetFullPath(fullPath);
        if (!resolvedPath.StartsWith(Path.GetFullPath(_basePath)))
        {
            _logger.LogWarning("Path traversal attempt detected for path: {Path}", fileUrl);
            throw new UnauthorizedAccessException("Invalid file path.");
        }

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    // ── Validation ────────────────────────────────────────────────────────

    public bool IsValidFile(IFormFile file, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (file == null || file.Length == 0)
        {
            errorMessage = "No file was provided or file is empty.";
            return false;
        }

        if (file.Length > _maxFileSizeBytes)
        {
            errorMessage = $"File size exceeds {_maxFileSizeBytes / 1024 / 1024}MB limit.";
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            errorMessage = $"File type '{ext}' is not allowed. Allowed: jpg, jpeg, png, pdf.";
            return false;
        }

        if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            errorMessage = $"Content type '{file.ContentType}' is not allowed.";
            return false;
        }

        // Read first few bytes to validate magic bytes (prevent MIME spoofing)
        using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = stream.Read(header, 0, header.Length);
        if (read < 4 || !HasValidMagicBytes(header, ext))
        {
            errorMessage = "File content does not match the declared file type.";
            return false;
        }

        return true;
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    private static string BuildRelativePath(string category, string userId, string ext)
    {
        var safeUserId = userId.Replace("/", "").Replace("\\", "").Replace("..", "");
        var guid = Guid.NewGuid().ToString("N");
        return Path.Combine(category, safeUserId, $"{guid}{ext}");
    }

    private static bool HasValidMagicBytes(byte[] fileHeader, string ext)
    {
        if (!MagicBytes.TryGetValue(ext, out var magic)) return true;
        for (int i = 0; i < magic.Length && i < fileHeader.Length; i++)
            if (fileHeader[i] != magic[i]) return false;
        return true;
    }

    private static void ValidateMagicBytes(byte[] fileBytes, string ext)
    {
        if (!MagicBytes.TryGetValue(ext, out var magic)) return;
        for (int i = 0; i < magic.Length; i++)
            if (fileBytes[i] != magic[i])
                throw new InvalidOperationException("File content does not match the declared file type.");
    }

    private static string MaskUserId(string userId) =>
        userId.Length > 8 ? userId[..4] + "****" + userId[^4..] : "****";
}
