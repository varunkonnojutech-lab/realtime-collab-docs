namespace CollabDocs.Api.Models;

public class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
    public int Version { get; set; }
}
