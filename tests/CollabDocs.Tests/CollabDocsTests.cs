using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CollabDocs.Api.Data;
using CollabDocs.Api.Models;
using CollabDocs.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CollabDocs.Tests;

public class CollabDocsTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(
                    d => d.ServiceType == typeof(DbContextOptions<CollabDocsDbContext>));
                services.Remove(descriptor);
                services.AddDbContext<CollabDocsDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
            }));
    }

    private static async Task<Guid> CreateDocumentAsync(HttpClient client, string title = "Test doc", string content = "Hello world")
    {
        var response = await client.PostAsJsonAsync("/api/documents", new { title, content });
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<Document>(Json);
        return doc!.Id;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateDocument_Returns201WithVersionOne()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/documents", new { title = "My doc", content = "Body" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<Document>(Json);
        Assert.NotNull(doc);
        Assert.Equal("My doc", doc!.Title);
        Assert.Equal("Body", doc.Content);
        Assert.Equal(1, doc.Version);
    }

    [Fact]
    public async Task ListDocuments_IncludesPreviewTruncatedTo120Chars()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var longContent = new string('x', 200);
        await CreateDocumentAsync(client, "Long doc", longContent);
        var response = await client.GetAsync("/api/documents");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>(Json);
        var item = items!.Single(i => i.GetProperty("title").GetString() == "Long doc");
        Assert.Equal(120, item.GetProperty("preview").GetString()!.Length);
        Assert.Equal(1, item.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task GetDocument_ReturnsDocumentAndDeleteRemovesIt()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var id = await CreateDocumentAsync(client);
        var get = await client.GetAsync($"/api/documents/{id}");
        get.EnsureSuccessStatusCode();
        var delete = await client.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var after = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task UpdateDocument_BumpsVersion()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var id = await CreateDocumentAsync(client);
        var response = await client.PutAsJsonAsync($"/api/documents/{id}",
            new { title = "Renamed", content = "New body" });
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<Document>(Json);
        Assert.Equal("Renamed", doc!.Title);
        Assert.Equal(2, doc.Version);
    }

    [Fact]
    public async Task UpdateDocument_WithStaleExpectedVersion_Returns409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var id = await CreateDocumentAsync(client);
        var response = await client.PutAsJsonAsync($"/api/documents/{id}",
            new { title = "Stale", content = "Nope", expectedVersion = 99 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var get = await client.GetAsync($"/api/documents/{id}");
        var doc = await get.Content.ReadFromJsonAsync<Document>(Json);
        Assert.Equal("Test doc", doc!.Title);
        Assert.Equal(1, doc.Version);
    }

    [Fact]
    public async Task UpdateDocument_WithMatchingExpectedVersion_Succeeds()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var id = await CreateDocumentAsync(client);
        var response = await client.PutAsJsonAsync($"/api/documents/{id}",
            new { title = "Fresh", content = "Yes", expectedVersion = 1 });
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<Document>(Json);
        Assert.Equal(2, doc!.Version);
    }

    [Fact]
    public async Task Comments_CreateListAndDelete()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var id = await CreateDocumentAsync(client);
        var create = await client.PostAsJsonAsync($"/api/documents/{id}/comments",
            new { author = "Ava", text = "Nice intro", anchorPosition = 5 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var comment = await create.Content.ReadFromJsonAsync<Comment>(Json);
        Assert.NotNull(comment);
        var list = await client.GetAsync($"/api/documents/{id}/comments");
        var comments = await list.Content.ReadFromJsonAsync<List<Comment>>(Json);
        Assert.Single(comments!);
        Assert.Equal("Ava", comments![0].Author);
        var delete = await client.DeleteAsync($"/api/documents/{id}/comments/{comment!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var after = await client.GetAsync($"/api/documents/{id}/comments");
        Assert.Empty(await after.Content.ReadFromJsonAsync<List<Comment>>(Json) ?? new());
    }

    [Fact]
    public void EditingService_AppliesInsertAndBumpsVersion()
    {
        var service = new DocumentEditingService();
        var doc = new Document { Id = Guid.NewGuid(), Title = "T", Content = "Hello world", Version = 3 };
        var result = service.ApplyEdit(doc, new EditDto { Type = "insert", Position = 5, Text = " brave", BaseVersion = 3 });
        Assert.True(result.Applied);
        Assert.Equal("Hello brave world", doc.Content);
        Assert.Equal(4, doc.Version);
    }

    [Fact]
    public void EditingService_AppliesDeleteAndClampsLength()
    {
        var service = new DocumentEditingService();
        var doc = new Document { Id = Guid.NewGuid(), Title = "T", Content = "Hello world", Version = 1 };
        var result = service.ApplyEdit(doc, new EditDto { Type = "delete", Position = 5, Length = 999, BaseVersion = 1 });
        Assert.True(result.Applied);
        Assert.Equal("Hello", doc.Content);
        Assert.Equal(2, doc.Version);
    }

    [Fact]
    public void EditingService_RejectsStaleBaseVersionWithoutChangingContent()
    {
        var service = new DocumentEditingService();
        var doc = new Document { Id = Guid.NewGuid(), Title = "T", Content = "Hello world", Version = 5 };
        var result = service.ApplyEdit(doc, new EditDto { Type = "insert", Position = 0, Text = "X", BaseVersion = 4 });
        Assert.False(result.Applied);
        Assert.Equal(5, result.CurrentVersion);
        Assert.Equal("Hello world", doc.Content);
        Assert.Equal(5, doc.Version);
    }

    [Fact]
    public void EditingService_ClampsOutOfRangePositions()
    {
        var service = new DocumentEditingService();
        var doc = new Document { Id = Guid.NewGuid(), Title = "T", Content = "abc", Version = 1 };
        service.ApplyEdit(doc, new EditDto { Type = "insert", Position = 100, Text = "!", BaseVersion = 1 });
        Assert.Equal("abc!", doc.Content);
        service.ApplyEdit(doc, new EditDto { Type = "insert", Position = -50, Text = "?", BaseVersion = 2 });
        Assert.Equal("?abc!", doc.Content);
    }

    [Fact]
    public void PresenceTracker_TracksAndRemovesMembers()
    {
        var tracker = new PresenceTracker();
        tracker.Add("doc-1", "conn-a", "Ava");
        tracker.Add("doc-1", "conn-b", "Liam");
        Assert.Equal(new[] { "Ava", "Liam" }, tracker.GetDisplayNames("doc-1"));
        tracker.Remove("doc-1", "conn-a");
        Assert.Equal(new[] { "Liam" }, tracker.GetDisplayNames("doc-1"));
        Assert.Equal(new[] { "doc-1" }, tracker.RemoveConnection("conn-b"));
        Assert.Empty(tracker.GetDisplayNames("doc-1"));
    }
}
