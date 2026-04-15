namespace HakaTech.Portal.Models.Domain;

public class KnowledgeBaseArticle
{
    public int    Id         { get; set; }
    public string Title      { get; set; } = string.Empty;
    public string Content    { get; set; } = string.Empty;  // HTML

    public int                  CategoryId { get; set; }
    public KnowledgeBaseCategory? Category { get; set; }

    public bool IsPublished { get; set; } = true;
    public bool IsFeatured  { get; set; }
    public int  ViewCount   { get; set; }

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt  { get; set; } = DateTime.UtcNow;

    public string           CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser   { get; set; }
}
