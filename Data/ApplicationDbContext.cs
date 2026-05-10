using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Data;

/// <summary>
/// Sovelluksen tietokantakonteksti. Tämä luokka edustaa "siltaa" C#-koodin ja
/// tietokannan välillä. Jokainen <see cref="DbSet{T}"/> vastaa yhtä taulua,
/// ja <see cref="OnModelCreating"/>-metodissa määritellään taulujen rakenne
/// (avaimet, sarakkeiden pituudet, suhteet eri taulujen välillä jne.).
///
/// Periytyy <see cref="IdentityDbContext{TUser}"/>-luokasta, jolloin saadaan
/// automaattisesti käyttöön Identityn käyttäjä-, rooli- ja kirjautumistaulut.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // Konstruktori: EF Core välittää asetukset (mm. yhteysmerkkijono).
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── Tietokantataulut ──────────────────────────────────────────
    // Jokainen DbSet vastaa yhtä taulua. Esim. Customers-taulun rivit
    // ovat Customer-olioita.
    public DbSet<Customer>       Customers      { get; set; } // asiakasyritykset
    public DbSet<Ticket>         Tickets        { get; set; } // tukipyynnöt (tiketit)
    public DbSet<TicketComment>  TicketComments { get; set; } // tikettien kommentit
    public DbSet<Invoice>        Invoices       { get; set; } // laskut
    public DbSet<InvoiceLine>    InvoiceLines   { get; set; } // laskurivit
    public DbSet<Contract>       Contracts      { get; set; } // sopimukset (esim. kk-veloitus)
    public DbSet<TicketAttachment>   TicketAttachments   { get; set; } // tikettien liitetiedostot
    public DbSet<InvoiceAttachment>  InvoiceAttachments  { get; set; } // laskujen liitetiedostot
    public DbSet<ServiceCatalogItem>    ServiceCatalogItems      { get; set; } // palvelukatalogi (myytävät palvelut)
    public DbSet<QuoteRequest>          QuoteRequests            { get; set; } // tarjouspyynnöt
    public DbSet<Announcement>          Announcements            { get; set; } // tiedotteet
    public DbSet<RemoteDesktopConnection>  RemoteDesktopConnections   { get; set; } // etätyöpöytäyhteydet
    public DbSet<KnowledgeBaseCategory>   KnowledgeBaseCategories    { get; set; } // tietopankin kategoriat
    public DbSet<KnowledgeBaseArticle>    KnowledgeBaseArticles      { get; set; } // tietopankin artikkelit
    public DbSet<TicketFeedback>          TicketFeedbacks            { get; set; } // tikettipalautteet
    public DbSet<AuditLog>                AuditLogs                  { get; set; } // audit-loki (kuka teki ja mitä)
    public DbSet<BookingSlot>             BookingSlots               { get; set; } // ajanvarausten aikaikkunat
    public DbSet<Booking>                 Bookings                   { get; set; } // varatut ajat

    /// <summary>
    /// EF Core kutsuu tätä metodia kun se rakentaa tietokantamallin.
    /// Täällä määritellään tarkemmin taulujen rakenne ja entiteettien
    /// väliset suhteet (1-moneen, monta-moneen jne.).
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // TÄRKEÄ: kutsutaan kantaluokkaa ENSIN, jotta Identityn omat taulut
        // (AspNetUsers, AspNetRoles ym.) konfiguroituvat oikein.
        base.OnModelCreating(builder);

        // ── Customer (asiakasyritys) ──────────────────────────────
        builder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);                                       // pääavain
            e.Property(c => c.CompanyName).IsRequired().HasMaxLength(200);
            e.Property(c => c.BusinessId).IsRequired().HasMaxLength(20);
            e.HasIndex(c => c.BusinessId).IsUnique();                  // y-tunnus on uniikki
        });

        // ── ApplicationUser → Customer ────────────────────────────
        // Jokainen käyttäjä voi kuulua yhdelle asiakkaalle (1-moneen-suhde).
        // Jos asiakas poistetaan, käyttäjien CustomerId nollataan
        // (käyttäjä ei katoa mukana).
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Customer)
             .WithMany(c => c.Users)
             .HasForeignKey(u => u.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Ticket (tukipyyntö) ───────────────────────────────────
        builder.Entity<Ticket>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(300);
            e.Property(t => t.Description).IsRequired();

            // Tiketin luoja — Restrict estää käyttäjän poiston
            // jos tällä on tikettejä (säilyttää historian).
            e.HasOne(t => t.CreatedByUser)
             .WithMany()
             .HasForeignKey(t => t.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            // Vastuuhenkilö (ylläpitäjä) — ei pakollinen.
            // Käyttäjän poiston yhteydessä kenttä nollataan.
            e.HasOne(t => t.AssignedToUser)
             .WithMany()
             .HasForeignKey(t => t.AssignedToUserId)
             .OnDelete(DeleteBehavior.SetNull);

            // Asiakkaan poistuessa myös sen tiketit poistetaan (cascade).
            e.HasOne(t => t.Customer)
             .WithMany(c => c.Tickets)
             .HasForeignKey(t => t.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TicketComment (kommentti tikettiin) ───────────────────
        builder.Entity<TicketComment>(e =>
        {
            e.HasKey(tc => tc.Id);
            e.Property(tc => tc.Content).IsRequired();

            // Tiketin poisto poistaa myös sen kommentit.
            e.HasOne(tc => tc.Ticket)
             .WithMany(t => t.Comments)
             .HasForeignKey(tc => tc.TicketId)
             .OnDelete(DeleteBehavior.Cascade);

            // Kirjoittajan käyttäjätili säilytetään (Restrict).
            e.HasOne(tc => tc.Author)
             .WithMany()
             .HasForeignKey(tc => tc.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Invoice (lasku) ───────────────────────────────────────
        builder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
            e.HasIndex(i => i.InvoiceNumber).IsUnique();   // laskunumero ei saa toistua
            e.Property(i => i.VatRate).HasPrecision(5, 4); // ALV-prosentti, esim. 0.2550 = 25,50 %

            // Asiakas täytyy olla — laskuja ei poisteta asiakkaan mukana
            // jotta talouskirjanpito säilyy.
            e.HasOne(i => i.Customer)
             .WithMany(c => c.Invoices)
             .HasForeignKey(i => i.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InvoiceLine (laskurivi) ───────────────────────────────
        builder.Entity<InvoiceLine>(e =>
        {
            e.HasKey(il => il.Id);
            e.Property(il => il.UnitPrice).HasPrecision(18, 2); // hinnan tarkkuus 2 desimaalia
            e.Property(il => il.Quantity).HasPrecision(10, 2);

            // Laskun poistaminen poistaa myös sen rivit.
            e.HasOne(il => il.Invoice)
             .WithMany(i => i.Lines)
             .HasForeignKey(il => il.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Contract (sopimus) ────────────────────────────────────
        builder.Entity<Contract>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.MonthlyPrice).HasPrecision(18, 2);

            // Asiakkaan poistuessa sopimuksetkin poistuvat.
            e.HasOne(c => c.Customer)
             .WithMany(cu => cu.Contracts)
             .HasForeignKey(c => c.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TicketAttachment (tiketin liite) ──────────────────────
        builder.Entity<TicketAttachment>(e =>
        {
            // Tiketin mukana liitteet poistuvat, mutta lataaja-tieto säilytetään.
            e.HasOne(a => a.Ticket)
             .WithMany(t => t.Attachments)
             .HasForeignKey(a => a.TicketId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.UploadedByUser)
             .WithMany()
             .HasForeignKey(a => a.UploadedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InvoiceAttachment (laskun liite) ──────────────────────
        builder.Entity<InvoiceAttachment>(e =>
        {
            e.HasOne(a => a.Invoice)
             .WithMany(i => i.Attachments)
             .HasForeignKey(a => a.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.UploadedByUser)
             .WithMany()
             .HasForeignKey(a => a.UploadedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ServiceCatalogItem (palvelukatalogin tuote) ───────────
        builder.Entity<ServiceCatalogItem>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(200);
            e.Property(s => s.Category).HasMaxLength(100);
            e.Property(s => s.Price).HasPrecision(18, 2);
        });

        // ── QuoteRequest (tarjouspyyntö) ──────────────────────────
        builder.Entity<QuoteRequest>(e =>
        {
            e.HasKey(q => q.Id);

            // Linkki palveluun, jota tarjousta pyydetään.
            e.HasOne(q => q.Service)
             .WithMany(s => s.QuoteRequests)
             .HasForeignKey(q => q.ServiceCatalogItemId)
             .OnDelete(DeleteBehavior.Restrict);

            // Asiakas, jolle tarjous tehdään.
            e.HasOne(q => q.Customer)
             .WithMany()
             .HasForeignKey(q => q.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            // Pyynnön luoja (käyttäjätili säilytetään historian vuoksi).
            e.HasOne(q => q.CreatedByUser)
             .WithMany()
             .HasForeignKey(q => q.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Announcement (tiedote) ────────────────────────────────
        builder.Entity<Announcement>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).IsRequired().HasMaxLength(300);
            e.Property(a => a.Content).IsRequired();

            e.HasOne(a => a.CreatedByUser)
             .WithMany()
             .HasForeignKey(a => a.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RemoteDesktopConnection (etätyöpöytäyhteyden tiedot) ──
        builder.Entity<RemoteDesktopConnection>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).IsRequired().HasMaxLength(200);
            e.Property(r => r.Hostname).IsRequired().HasMaxLength(500);
            e.Property(r => r.Username).HasMaxLength(200);
            // Salasana säilytetään salattuna (Data Protection API).
            e.Property(r => r.EncryptedPassword).HasMaxLength(2000);
            e.Property(r => r.Security).HasMaxLength(50);
            e.Property(r => r.Notes).HasMaxLength(2000);

            e.HasOne(r => r.Customer)
             .WithMany(c => c.RemoteDesktopConnections)
             .HasForeignKey(r => r.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TicketFeedback (asiakaspalaute tiketistä) ─────────────
        builder.Entity<TicketFeedback>(e =>
        {
            e.HasKey(f => f.Id);
            // Yksi palaute per tiketti — uniikki indeksi.
            e.HasIndex(f => f.TicketId).IsUnique();
            e.Property(f => f.Comment).HasMaxLength(2000);

            // 1-1 -suhde: tiketillä on enintään yksi palaute.
            e.HasOne(f => f.Ticket)
             .WithOne(t => t.Feedback)
             .HasForeignKey<TicketFeedback>(f => f.TicketId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.User)
             .WithMany()
             .HasForeignKey(f => f.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AuditLog (lokimerkintä) ───────────────────────────────
        // Audit-loki tallentaa kuka teki mitä, missä ja milloin.
        // Tärkeää tietoturvan ja tutkinnan kannalta.
        builder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityType).HasMaxLength(50);
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.Property(a => a.UserEmail).HasMaxLength(256);
            e.Property(a => a.Details).HasMaxLength(1000);
            e.Property(a => a.IpAddress).HasMaxLength(50);
            // Indeksit nopeuttavat hakuja aikaleiman ja käyttäjän mukaan.
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.UserId);
        });

        // ── KnowledgeBaseCategory (tietopankin kategoria) ─────────
        builder.Entity<KnowledgeBaseCategory>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Description).HasMaxLength(500);
        });

        // ── KnowledgeBaseArticle (tietopankin artikkeli) ──────────
        builder.Entity<KnowledgeBaseArticle>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).IsRequired().HasMaxLength(300);
            e.Property(a => a.Content).IsRequired();

            // Artikkelin kategoria — Restrict estää kategorian poiston
            // jos siinä on artikkeleita.
            e.HasOne(a => a.Category)
             .WithMany(c => c.Articles)
             .HasForeignKey(a => a.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.CreatedByUser)
             .WithMany()
             .HasForeignKey(a => a.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── BookingSlot (varattavissa oleva aikaikkuna) ───────────
        builder.Entity<BookingSlot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Title).IsRequired().HasMaxLength(200);
            e.Property(s => s.Description).HasMaxLength(1000);
            // Indeksoidaan alkuaika, koska kalenterihaut tehdään aikajärjestyksessä.
            e.HasIndex(s => s.StartTime);

            e.HasOne(s => s.CreatedByUser)
             .WithMany()
             .HasForeignKey(s => s.CreatedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Booking (varaus aikaikkunaan) ─────────────────────────
        builder.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Notes).HasMaxLength(1000);
            e.Property(b => b.CancellationReason).HasMaxLength(500);

            // Slotin poistuessa sen varaukset poistuvat.
            e.HasOne(b => b.BookingSlot)
             .WithMany(s => s.Bookings)
             .HasForeignKey(b => b.BookingSlotId)
             .OnDelete(DeleteBehavior.Cascade);

            // Asiakas ja varaaja säilytetään (Restrict).
            e.HasOne(b => b.Customer)
             .WithMany()
             .HasForeignKey(b => b.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.User)
             .WithMany()
             .HasForeignKey(b => b.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
