using Microsoft.AspNetCore.Http;

namespace HakaTech.Portal.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(IFormFile file, string directoryName);
    void DeleteFile(string filePath);
}
