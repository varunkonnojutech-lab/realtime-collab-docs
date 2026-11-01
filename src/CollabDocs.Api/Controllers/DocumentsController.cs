using CollabDocs.Api.Data;
using CollabDocs.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollabDocs.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly CollabDocsDbContext _db;

    public DocumentsController(CollabDocsDbContext db)
    {
        _db = db;
    }

    private static object ToListItem(Document d) => new
    {
        id = d.Id,
        title = d.Title,
        updatedAtUtc = d.UpdatedAtUtc,
        version = d.Version,
        preview = d.Content.Length <= 120 ? d.Content : d.Content[..120]
    };

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var documents = await _db.Documents
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync();
        return Ok(documents.Select(ToListItem));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content ?? string.Empty,
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var document = await _db.Documents.FindAsync(id);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest request)
    {
        var document = await _db.Documents.FindAsync(id);
        if (document is null)
        {
            return NotFound();
        }

        if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != document.Version)
        {
            return Conflict(new
            {
                message = $"Version conflict: expected {request.ExpectedVersion.Value} but the document is at {document.Version}.",
                currentVersion = document.Version
            });
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        document.Title = request.Title.Trim();
        document.Content = request.Content;
        document.Version += 1;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(document);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var document = await _db.Documents.FindAsync(id);
        if (document is null)
        {
            return NotFound();
        }

        _db.Documents.Remove(document);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> ListComments(Guid id)
    {
        if (!await _db.Documents.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        var comments = await _db.Comments
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync();
        return Ok(comments);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> CreateComment(Guid id, [FromBody] CreateCommentRequest request)
    {
        if (!await _db.Documents.AnyAsync(d => d.Id == id))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Author) || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Author and text are required.");
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            DocumentId = id,
            Author = request.Author.Trim(),
            Text = request.Text,
            AnchorPosition = request.AnchorPosition,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ListComments), new { id }, comment);
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.DocumentId == id);
        if (comment is null)
        {
            return NotFound();
        }

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
