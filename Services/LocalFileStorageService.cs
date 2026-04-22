using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HakaTech.Portal.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _uploadsRoot;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
        _uploadsRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "uploads"));
        Directory.CreateDirectory(_uploadsRoot);
    }

    public async Task<string> SaveFileAsync(IFormFile file, string directoryName)
    {
        if (file == null || file.Length == 0) return string.Empty;

        var safeDir = SanitizeSegment(directoryName);
        var uploadDir = Path.Combine(_uploadsRoot, safeDir);
        Directory.CreateDirectory(uploadDir);

        var originalName = Path.GetFileName(file.FileName);
        var fileName = $"{Guid.NewGuid():N}_{SanitizeSegment(originalName)}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        return $"{safeDir}/{fileName}";
    }

    public void DeleteFile(string filePath)
    {
        var fullPath = ResolveSafePath(filePath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string? ResolveSafePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        var trimmed = storedPath.TrimStart('/', '\\');

        // Yhteensopivuus vanhoille "uploads/..."-poluille
        if (trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("uploads/".Length);
        }

        var candidate = Path.GetFullPath(Path.Combine(_uploadsRoot, trimmed));

        var rootWithSep = _uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _uploadsRoot
            : _uploadsRoot + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            return null;

        return candidate;
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(segment.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return clean.Replace("..", "_");
    }
}
