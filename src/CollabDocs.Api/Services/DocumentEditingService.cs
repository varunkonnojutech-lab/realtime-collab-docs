using CollabDocs.Api.Models;

namespace CollabDocs.Api.Services;

/// <summary>
/// Result of applying one realtime edit to a document.
/// </summary>
public sealed class EditResult
{
    public bool Applied { get; init; }
    public int CurrentVersion { get; init; }
    public string? RejectionReason { get; init; }

    public static EditResult Success(int newVersion) =>
        new() { Applied = true, CurrentVersion = newVersion };

    public static EditResult Rejected(int currentVersion, string reason) =>
        new() { Applied = false, CurrentVersion = currentVersion, RejectionReason = reason };
}

/// <summary>
/// Pure edit-application logic, shared by the SignalR hub and unit-testable
/// without a live connection. Mutates the supplied document in place; the
/// caller is responsible for persisting it.
/// </summary>
public class DocumentEditingService
{
    public EditResult ApplyEdit(Document document, EditDto edit)
    {
        if (edit.BaseVersion != document.Version)
        {
            return EditResult.Rejected(
                document.Version,
                $"Stale base version: edit was based on {edit.BaseVersion} but the document is at {document.Version}.");
        }

        var content = document.Content ?? string.Empty;
        var position = Math.Clamp(edit.Position, 0, content.Length);

        switch (edit.Type)
        {
            case "insert":
                content = content.Insert(position, edit.Text ?? string.Empty);
                break;
            case "delete":
            {
                var length = Math.Clamp(edit.Length ?? 0, 0, content.Length - position);
                content = content.Remove(position, length);
                break;
            }
            default:
                return EditResult.Rejected(document.Version, $"Unknown edit type '{edit.Type}'.");
        }

        document.Content = content;
        document.Version += 1;
        document.UpdatedAtUtc = DateTime.UtcNow;
        return EditResult.Success(document.Version);
    }
}
