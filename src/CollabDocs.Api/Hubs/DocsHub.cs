using CollabDocs.Api.Data;
using CollabDocs.Api.Models;
using CollabDocs.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CollabDocs.Api.Hubs;

/// <summary>
/// Realtime collaboration hub, mounted at /hubs/docs.
///
/// Client -> server methods:
///   JoinDocument(documentId, displayName)
///   LeaveDocument(documentId)
///   SendEdit(documentId, EditDto)
///   CursorMove(documentId, position)
///
/// Server -> client events:
///   UserJoined   { documentId, displayName, connectionId }
///   UserLeft     { documentId, displayName, connectionId }
///   PresenceList (array of display names currently in the document)
///   CursorMoved  { documentId, displayName, position }
///   EditApplied  { documentId, edit, version, content }
///   EditRejected { documentId, currentVersion, content } (caller only)
/// </summary>
public class DocsHub : Hub
{
    public const string GroupPrefix = "doc:";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DocumentEditingService _editingService;
    private readonly PresenceTracker _presence;

    public DocsHub(
        IServiceScopeFactory scopeFactory,
        DocumentEditingService editingService,
        PresenceTracker presence)
    {
        _scopeFactory = scopeFactory;
        _editingService = editingService;
        _presence = presence;
    }

    public static string GroupName(string documentId) => $"{GroupPrefix}{documentId}";

    public async Task JoinDocument(string documentId, string displayName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(documentId));
        _presence.Add(documentId, Context.ConnectionId, displayName);

        await Clients.Group(GroupName(documentId)).SendAsync("UserJoined", new
        {
            documentId,
            displayName,
            connectionId = Context.ConnectionId
        });
        await Clients.Group(GroupName(documentId)).SendAsync(
            "PresenceList", _presence.GetDisplayNames(documentId));
    }

    public async Task LeaveDocument(string documentId)
    {
        var displayName = _presence.GetDisplayName(documentId, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(documentId));
        _presence.Remove(documentId, Context.ConnectionId);

        await Clients.Group(GroupName(documentId)).SendAsync("UserLeft", new
        {
            documentId,
            displayName,
            connectionId = Context.ConnectionId
        });
        await Clients.Group(GroupName(documentId)).SendAsync(
            "PresenceList", _presence.GetDisplayNames(documentId));
    }

    public async Task SendEdit(string documentId, EditDto edit)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollabDocsDbContext>();

        if (!Guid.TryParse(documentId, out var id))
        {
            return;
        }

        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null)
        {
            return;
        }

        var result = _editingService.ApplyEdit(document, edit);
        if (!result.Applied)
        {
            await Clients.Caller.SendAsync("EditRejected", new
            {
                documentId,
                currentVersion = document.Version,
                content = document.Content
            });
            return;
        }

        await db.SaveChangesAsync();

        await Clients.Group(GroupName(documentId)).SendAsync("EditApplied", new
        {
            documentId,
            edit,
            version = document.Version,
            content = document.Content
        });
    }

    public async Task CursorMove(string documentId, int position)
    {
        var displayName = _presence.GetDisplayName(documentId, Context.ConnectionId);
        await Clients.Group(GroupName(documentId)).SendAsync("CursorMoved", new
        {
            documentId,
            displayName,
            position
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var affectedDocuments = _presence.RemoveConnection(Context.ConnectionId);
        foreach (var documentId in affectedDocuments)
        {
            await Clients.Group(GroupName(documentId)).SendAsync("UserLeft", new
            {
                documentId,
                displayName = (string?)null,
                connectionId = Context.ConnectionId
            });
            await Clients.Group(GroupName(documentId)).SendAsync(
                "PresenceList", _presence.GetDisplayNames(documentId));
        }

        await base.OnDisconnectedAsync(exception);
    }
}
