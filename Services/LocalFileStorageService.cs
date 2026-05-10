using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HakaTech.Portal.Services;

/// <summary>
/// Tiedostojen tallennus paikalliselle levylle. Tiedostot menevät
/// App_Data/uploads-kansion alle, ja jokainen luokka (esim. tikettiliitteet)
/// saa oman alikansion.
///
/// Tärkeä tietoturvatoiminto: ResolveSafePath estää path traversal -hyökkäykset
/// (esim. polku "../../etc/passwd"), jotka voisivat vuotaa muita tiedostoja.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _uploadsRoot;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
        // Uploads-juurikansio sovelluksen sisällä. App_Data on sopiva paikka
        // koska se on oletuksena ASP.NET Coressa suojattu suoralta latauksia.
        _uploadsRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "uploads"));
        Directory.CreateDirectory(_uploadsRoot);
    }

    /// <summary>
    /// Tallentaa selaimesta lähetetyn tiedoston levylle. Tiedostonimi
    /// satunnaistetaan GUIDilla, jotta sama nimi ei aiheuta törmäyksiä.
    /// </summary>
    public async Task<string> SaveFileAsync(IFormFile file, string directoryName)
    {
        // Tyhjää tiedostoa ei tallenneta.
        if (file == null || file.Length == 0) return string.Empty;

        // Puhdistetaan kansion nimi turvalliseksi, ja luodaan tarvittaessa.
        var safeDir = SanitizeSegment(directoryName);
        var uploadDir = Path.Combine(_uploadsRoot, safeDir);
        Directory.CreateDirectory(uploadDir);

        // Etuliitteenä uniikki GUID estää tiedostonimien törmäykset.
        var originalName = Path.GetFileName(file.FileName);
        var fileName = $"{Guid.NewGuid():N}_{SanitizeSegment(originalName)}";
        var filePath = Path.Combine(uploadDir, fileName);

        // CreateNew = epäonnistuu jos tiedosto jo löytyy (lisäsuoja).
        using (var stream = new FileStream(filePath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        // Palautetaan suhteellinen polku, joka tallennetaan kantaan.
        return $"{safeDir}/{fileName}";
    }

    /// <summary>Poistaa tiedoston turvallisesti — ei tee mitään jos polku on virheellinen.</summary>
    public void DeleteFile(string filePath)
    {
        var fullPath = ResolveSafePath(filePath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    /// <summary>
    /// Muuntaa tallennetun suhteellisen polun absoluuttiseksi poluksi.
    /// Tarkistaa että lopullinen polku on uploads-kansion sisällä —
    /// estää path traversal -hyökkäykset kuten "../../secret.txt".
    /// </summary>
    public string? ResolveSafePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        // Poistetaan mahdolliset alkukenoviivat.
        var trimmed = storedPath.TrimStart('/', '\\');

        // Taaksepäin yhteensopivuus: aiemmin tallennetut polut alkoivat "uploads/...".
        if (trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("uploads/".Length);
        }

        // Path.GetFullPath ratkaisee mahdolliset ".."-osat normalisoiduksi poluksi.
        var candidate = Path.GetFullPath(Path.Combine(_uploadsRoot, trimmed));

        // Lisätään hakemistoerotin loppuun, ettei "uploadsBoot" osu samaan alkuun kuin "uploads".
        var rootWithSep = _uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _uploadsRoot
            : _uploadsRoot + Path.DirectorySeparatorChar;

        // Jos lopullinen polku ei alka uploads-juurikansiosta → hylätään.
        if (!candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            return null;

        return candidate;
    }

    /// <summary>
    /// Korvaa tiedostonimissä kielletyt merkit alaviivalla, ja
    /// estää ".."-sekvenssin (path traversal -lisäsuoja).
    /// </summary>
    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(segment.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return clean.Replace("..", "_");
    }
}
