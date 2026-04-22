using Microsoft.AspNetCore.Http;

namespace HakaTech.Portal.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string directoryName);
    void DeleteFile(string filePath);

    /// <summary>
    /// Resolves a stored relative path to an absolute path inside the uploads root.
    /// Returns null if the path escapes the uploads directory or does not exist.
    /// </summary>
    string? ResolveSafePath(string? storedPath);
}
