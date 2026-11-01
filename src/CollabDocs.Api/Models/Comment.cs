using System.Text.Json.Serialization;

namespace CollabDocs.Api.Models;

public class Comment
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int? AnchorPosition { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    [JsonIgnore]
    public Document? Document { get; set; }
}
