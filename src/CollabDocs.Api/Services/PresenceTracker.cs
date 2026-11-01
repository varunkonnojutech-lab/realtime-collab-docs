using System.Collections.Concurrent;

namespace CollabDocs.Api.Services;

/// <summary>
/// In-memory presence: which connections are in which document, and under
/// which display name. Keyed by document id, then connection id.
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _documents = new();

    public void Add(string documentId, string connectionId, string displayName) =>
        _documents.GetOrAdd(documentId, _ => new ConcurrentDictionary<string, string>())
            .AddOrUpdate(connectionId, displayName, (_, _) => displayName);

    public void Remove(string documentId, string connectionId)
    {
        if (_documents.TryGetValue(documentId, out var members))
        {
            members.TryRemove(connectionId, out _);
            if (members.IsEmpty)
            {
                _documents.TryRemove(documentId, out _);
            }
        }
    }

    public string[] GetDisplayNames(string documentId) =>
        _documents.TryGetValue(documentId, out var members)
            ? members.Values.OrderBy(name => name).ToArray()
            : Array.Empty<string>();

    public string? GetDisplayName(string documentId, string connectionId) =>
        _documents.TryGetValue(documentId, out var members)
        && members.TryGetValue(connectionId, out var name)
            ? name
            : null;

    /// <summary>Removes a connection from every document; returns the affected document ids.</summary>
    public string[] RemoveConnection(string connectionId)
    {
        var affected = new List<string>();
        foreach (var (documentId, members) in _documents)
        {
            if (members.TryRemove(connectionId, out _))
            {
                affected.Add(documentId);
                if (members.IsEmpty)
                {
                    _documents.TryRemove(documentId, out _);
                }
            }
        }
        return affected.ToArray();
    }
}
