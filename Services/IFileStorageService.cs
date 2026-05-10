using Microsoft.AspNetCore.Http;

namespace HakaTech.Portal.Services;

/// <summary>
/// Tiedostojen tallennuspalvelun rajapinta — abstrahoi missä ja miten
/// tiedostot säilötään. Nykyinen toteutus käyttää paikallista levyä,
/// mutta voidaan tulevaisuudessa korvata pilvitallennuksella.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Tallentaa ladatun tiedoston annettuun alikansioon. Palauttaa
    /// suhteellisen polun, joka tallennetaan tietokantaan.
    /// </summary>
    Task<string> SaveFileAsync(IFormFile file, string directoryName);

    /// <summary>Poistaa tiedoston levyltä. Ei nosta poikkeusta jos tiedostoa ei löydy.</summary>
    void DeleteFile(string filePath);

    /// <summary>
    /// Muuntaa kannassa olevan suhteellisen polun absoluuttiseksi poluksi
    /// uploads-juurikansion sisällä. Palauttaa null jos polku yrittää karata
    /// uploads-kansiosta (path traversal -hyökkäyksen esto) tai tiedostoa ei ole.
    /// </summary>
    string? ResolveSafePath(string? storedPath);
}
