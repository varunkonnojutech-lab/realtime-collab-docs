using CollabDocs.Api.Data;
using CollabDocs.Api.Hubs;
using CollabDocs.Api.Models;
using CollabDocs.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CollabDocsDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=collabdocs.db"));

builder.Services.AddScoped<DocumentEditingService>();
builder.Services.AddSingleton<PresenceTracker>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://localhost:3000", "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.MapGet("/health", () => Results.Text("ok"));

app.MapControllers();
app.MapHub<DocsHub>("/hubs/docs");

// Create the database and seed sample content on first run only.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CollabDocsDbContext>();
    db.Database.EnsureCreated();

    if (!db.Documents.Any())
    {
        var welcome = new Document
        {
            Id = Guid.NewGuid(),
            Title = "Welcome to CollabDocs",
            Content = "Welcome to CollabDocs! This is a realtime collaborative document editor. " +
                "Open the same document in two browser windows and watch edits appear live. " +
                "Use comments to leave feedback anchored to a position in the text, and see " +
                "who else is editing with you through live presence and cursors.",
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        var meetingNotes = new Document
        {
            Id = Guid.NewGuid(),
            Title = "Team meeting notes",
            Content = "Team meeting notes\n\n- Sprint planning: pick up the realtime sync milestone.\n" +
                "- Review the new commenting flow before Friday.\n" +
                "- Action: demo the collaborative editor at the next all-hands.",
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        db.Documents.AddRange(welcome, meetingNotes);
        db.Comments.AddRange(
            new Comment
            {
                Id = Guid.NewGuid(),
                DocumentId = meetingNotes.Id,
                Author = "Ava",
                Text = "Can we move the demo earlier in the agenda?",
                AnchorPosition = 120,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Comment
            {
                Id = Guid.NewGuid(),
                DocumentId = meetingNotes.Id,
                Author = "Liam",
                Text = "The commenting flow review is done — looks good.",
                AnchorPosition = 60,
                CreatedAtUtc = DateTime.UtcNow
            });
        db.SaveChanges();
    }
}

app.Run();

public partial class Program
{
}
