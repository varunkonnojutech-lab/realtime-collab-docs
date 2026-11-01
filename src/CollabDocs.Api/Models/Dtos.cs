namespace CollabDocs.Api.Models;

/// <summary>One realtime edit operation sent over SignalR.</summary>
public class EditDto
{
    /// <summary>"insert" or "delete".</summary>
    public string Type { get; set; } = string.Empty;
    public int Position { get; set; }
    public string? Text { get; set; }
    public int? Length { get; set; }
    public int BaseVersion { get; set; }
}

public class CreateDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public class UpdateDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? ExpectedVersion { get; set; }
}

public class CreateCommentRequest
{
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int? AnchorPosition { get; set; }
}
