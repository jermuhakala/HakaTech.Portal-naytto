using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HakaTech.Portal.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string directoryName)
    {
        if (file == null || file.Length == 0) return string.Empty;

        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", directoryName);
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/{directoryName}/{fileName}";
    }

    public void DeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var fullPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
