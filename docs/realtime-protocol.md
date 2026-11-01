# Realtime protocol

How live collaboration works in CollabDocs. REST owns document CRUD and
comments; SignalR owns everything that has to arrive *now*.

## Hub

Connect to `/hubs/docs` with the `@microsoft/signalr` client
(`HubConnectionBuilder().withUrl("/hubs/docs").build()`).

## Client methods (browser → server)

| Method | Arguments | Behavior |
| ------ | --------- | -------- |
| `JoinDocument` | `documentId` (string), `displayName` (string) | Adds the connection to the document's SignalR group, broadcasts `UserJoined`, then broadcasts the updated `PresenceList` to the group. |
| `LeaveDocument` | `documentId` (string) | Removes the connection from the group, broadcasts `UserLeft`, then broadcasts the updated `PresenceList`. Leaving without calling this (e.g. closing the tab) is handled the same way on disconnect. |
| `SendEdit` | `documentId` (string), `edit` (EditOperation) | Applies the edit if `edit.baseVersion` matches the document's current version; on success the version increments and `EditApplied` is broadcast to the group (including the sender, so all clients converge on the same version). On a stale version the sender alone receives `EditRejected`. Edits for an unknown document id or a missing document are silently ignored. |
| `CursorMove` | `documentId` (string), `position` (number) | Broadcasts `CursorMoved` to the group. |

## Server events (server → browser)

| Event | Payload | Meaning |
| ----- | ------- | ------- |
| `EditApplied` | `{ documentId, edit, version, content }` | An edit was accepted; `version` is the document's new version and `content` is the resulting full body. Clients apply `edit` (or replace local content with `content`) and set their local version to `version`. |
| `EditRejected` | `{ documentId, currentVersion, content }` | The sender's edit was based on a stale version. The payload already carries the fresh `content`, so the client can reload without an extra REST round-trip. |
| `PresenceList` | `string[]` — display names of everyone currently in the document | Broadcast to the whole group after every join, leave, and disconnect, so clients can simply replace their collaborator list. |
| `UserJoined` | `{ documentId, displayName, connectionId }` | A collaborator entered the document. |
| `UserLeft` | `{ documentId, displayName, connectionId }` | A collaborator left (or disconnected; `displayName` is null on disconnect if the name was never recorded). |
| `CursorMoved` | `{ documentId, displayName, position }` | A collaborator's cursor moved; `position` is the character offset in the document body. |

## Edit operation shape

SignalR edits are **operations**, not full bodies. The client diffs its old
and new text (common prefix/suffix) and sends one insert and/or one
delete per change:

```json
{ "type": "insert", "position": 42, "text": "hello", "baseVersion": 3 }
{ "type": "delete", "position": 42, "length": 5, "baseVersion": 3 }
```

| Field | Meaning |
| ----- | ------- |
| `type` | `"insert"` or `"delete"` |
| `position` | Character offset in the document body where the change applies |
| `text` | Inserted text (insert only) |
| `length` | Number of characters removed (delete only) |
| `baseVersion` | The document version the client based this edit on |

The REST `PUT /api/documents/{id}` is different: it sends the full
`{ title, content, expectedVersion }` and behaves the same way on
version mismatch (409). So: full-body saves over REST, granular
operations over SignalR, one version counter for both.

## Version and conflict behavior

- Every document has an integer `version`, incremented by 1 on every
  accepted edit (REST or SignalR share the one counter).
- A REST `PUT /api/documents/{id}` and a SignalR `SendEdit` both take an
  expected version and both behave the same way:
  - **Match** → the edit is saved, the version increments by 1, and the
    new version is returned/broadcast.
  - **Mismatch** → the edit is rejected. REST returns **409 Conflict**
    with the current version; SignalR sends `EditRejected` to the caller
    only. Nothing is overwritten.
- The client contract on rejection: reload the document, show the user
  what changed if possible, and let them re-apply their edit. Never
  retry blindly with the new version — the user may have been editing
  stale text.

## Presence model

- Presence is per document, not global: joining a document subscribes
  you to that document's group only.
- A user is identified within a document by their SignalR `connectionId`
  (plus a display name chosen in the UI). There is no auth, so display
  names are self-asserted — see the README's Security note.
- Presence is ephemeral: it lives only in the hub's in-memory group
  membership. Restarting the API clears it; documents and comments in
  SQLite survive.
- Cursor positions are offsets into the document body. Clients render
  other users' cursors from `CursorMoved` events and remove them on
  `UserLeft`.
