// Nimiavaruus.
namespace HakaTech.Portal.Models.Domain;

/// <summary>
/// Tietopankin artikkeli. Kuuluu johonkin kategoriaan ja sisältää
/// HTML-muotoisen sisällön. Asiakkaat lukevat näitä self-service-ohjeina.
/// </summary>
// Tietopankki (Knowledge Base, KB) on ohjepankki jonka avulla asiakas
// voi ratkaista ongelmia itse ennen tiketin luomista.
// Artikkeleja käytetään myös tiketin luontilomakkeella: kun asiakas kirjoittaa otsikkoa,
// haetaan vastaavia artikkeleita ja ehdotetaan niitä ratkaisuksi.
public class KnowledgeBaseArticle
{
    // Pääavain.
    public int    Id         { get; set; }
    // Artikkelin otsikko — lyhyt ja kuvaava.
    public string Title      { get; set; } = string.Empty;

    /// <summary>HTML-muotoinen sisältö. Sanitoidaan ennen tallennusta XSS-suojan vuoksi.</summary>
    // Sisältö voi sisältää HTML-muotoilua: otsikoita, listoja, linkkejä, kuvia.
    // XSS (Cross-Site Scripting) = haavoittuvuus, jossa haitallinen JavaScript
    // pääsee suorittumaan käyttäjän selaimessa. Siksi sisältö sanitoidaan
    // Ganss.Xss-kirjastolla ennen tallennusta: sallitut tagit (p, h1, ul, img) pidetään,
    // vaaralliset (script, iframe, onerror) poistetaan.
    public string Content    { get; set; } = string.Empty;

    // ── Kategoria, johon artikkeli kuuluu ─────────────────────────────────────
    // Viiteavain KnowledgeBaseCategory-tauluun.
    public int                  CategoryId { get; set; }
    // Navigaatio kategoriaan.
    public KnowledgeBaseCategory? Category { get; set; }

    /// <summary>Onko artikkeli julkaistu. False = vain admin näkee (luonnos).</summary>
    // Sama "draft/julkaistu" -malli kuin Announcement-luokassa.
    public bool IsPublished { get; set; } = true;

    /// <summary>Korostetaanko artikkeli etusivulla / kategorian alussa.</summary>
    // IsFeatured = true → artikkeli näkyy etusivun "Suositellut artikkelit" -osiossa.
    // Adminin tapa nostaa tärkeimmät ohjeet esille.
    public bool IsFeatured  { get; set; }

    /// <summary>Lukukerrat — kasvaa joka näytöllä, auttaa tunnistamaan suosituimmat artikkelit.</summary>
    // Joka kerta kun artikkeli avataan, KnowledgeBaseController kasvattaa ViewCount-arvoa yhdellä.
    // Käytetään suosittujen artikkelien listaamiseen ja "katso myös" -ehdotuksissa.
    public int  ViewCount   { get; set; }

    // Luonti- ja muokkausaikaleimat.
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    // UpdatedAt = päivitetään aina kun artikkelia muokataan.
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    // Kuka artikkelin loi — näkyy artikkelin alaosassa ("Kirjoittanut: ...").
    public string           CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser   { get; set; }
}
