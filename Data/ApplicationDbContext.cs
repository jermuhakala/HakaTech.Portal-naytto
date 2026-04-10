using HakaTech.Portal.Models.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HakaTech.Portal.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Customer>       Customers      { get; set; }
    public DbSet<Ticket>         Tickets        { get; set; }
    public DbSet<TicketComment>  TicketComments { get; set; }
    public DbSet<Invoice>        Invoices       { get; set; }
    public DbSet<InvoiceLine>    InvoiceLines   { get; set; }
    public DbSet<Contract>       Contracts      { get; set; }
    public DbSet<TicketAttachment>   TicketAttachments   { get; set; }
    public DbSet<InvoiceAttachment>  InvoiceAttachments  { get; set; }
    public DbSet<ServiceCatalogItem> ServiceCatalogItems { get; set; }
    public DbSet<QuoteRequest>       QuoteRequests       { get; set; }
    public DbSet<Announcement>       Announcements       { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // TÄRKEÄ – Identity-taulut

        // ── Customer ──────────────────────────────────────────
        builder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.CompanyName).IsRequired().HasMaxLength(200);
            e.Property(c => c.BusinessId).IsRequired().HasMaxLength(20);
            e.HasIndex(c => c.BusinessId).IsUnique();
        });

        // ── ApplicationUser → Customer ────────────────────────
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Customer)
             .WithMany(c => c.Users)
             .HasForeignKey(u => u.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Ticket ────────────────────────────────────────────
        builder.Entity<Ticket>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(300);
            e.Property(t => t.Description).IsRequired();

            // Tiketin luonut käyttäjä
            e.HasOne(t => t.CreatedByUser)
             .WithMany()
             .HasForeignKey(t => t.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            // Vastuuhenkilö (admin) – ei pakollinen
            e.HasOne(t => t.AssignedToUser)
             .WithMany()
             .HasForeignKey(t => t.AssignedToUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.Customer)
             .WithMany(c => c.Tickets)
             .HasForeignKey(t => t.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TicketComment ─────────────────────────────────────
        builder.Entity<TicketComment>(e =>
        {
            e.HasKey(tc => tc.Id);
            e.Property(tc => tc.Content).IsRequired();

            e.HasOne(tc => tc.Ticket)
             .WithMany(t => t.Comments)
             .HasForeignKey(tc => tc.TicketId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(tc => tc.Author)
             .WithMany()
             .HasForeignKey(tc => tc.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Invoice ───────────────────────────────────────────
        builder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.Property(i => i.VatRate).HasPrecision(5, 4);

            e.HasOne(i => i.Customer)
             .WithMany(c => c.Invoices)
             .HasForeignKey(i => i.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InvoiceLine ───────────────────────────────────────
        builder.Entity<InvoiceLine>(e =>
        {
            e.HasKey(il => il.Id);
            e.Property(il => il.UnitPrice).HasPrecision(18, 2);
            e.Property(il => il.Quantity).HasPrecision(10, 2);

            e.HasOne(il => il.Invoice)
             .WithMany(i => i.Lines)
             .HasForeignKey(il => il.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Contract ──────────────────────────────────────────
        builder.Entity<Contract>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.MonthlyPrice).HasPrecision(18, 2);

            e.HasOne(c => c.Customer)
             .WithMany(cu => cu.Contracts)
             .HasForeignKey(c => c.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── TicketAttachment ───────────────────────────────────
        builder.Entity<TicketAttachment>(e =>
        {
            e.HasOne(a => a.Ticket)
             .WithMany(t => t.Attachments)
             .HasForeignKey(a => a.TicketId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.UploadedByUser)
             .WithMany()
             .HasForeignKey(a => a.UploadedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── InvoiceAttachment ──────────────────────────────────
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

        // ── ServiceCatalogItem ─────────────────────────────────
        builder.Entity<ServiceCatalogItem>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).IsRequired().HasMaxLength(200);
            e.Property(s => s.Category).HasMaxLength(100);
            e.Property(s => s.Price).HasPrecision(18, 2);
        });

        // ── QuoteRequest ───────────────────────────────────────
        builder.Entity<QuoteRequest>(e =>
        {
            e.HasKey(q => q.Id);

            e.HasOne(q => q.Service)
             .WithMany(s => s.QuoteRequests)
             .HasForeignKey(q => q.ServiceCatalogItemId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(q => q.Customer)
             .WithMany()
             .HasForeignKey(q => q.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(q => q.CreatedByUser)
             .WithMany()
             .HasForeignKey(q => q.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Announcement ───────────────────────────────────────
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
    }
}
