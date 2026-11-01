# Architecture

## Overview

CollabDocs is two processes:

```
Browser (React 18 + Vite + TypeScript)
  │  REST /api/documents, /api/documents/{id}/comments
  │  SignalR /hubs/docs
  ▼
ASP.NET Core 8 API
  ├── REST controllers — document CRUD, comments
  ├── SignalR hub — edits, cursors, presence (one group per document)
  └── EF Core + SQLite — documents, comments, versions
```

## Why this shape

- **REST for CRUD, SignalR for live.** Reads, writes, and comments go
  through plain REST so they are easy to test and debug. Only the
  things that must push to other browsers — edits, cursors, presence —
  go through the hub.
- **Full-body saves over REST, granular ops over SignalR.** A REST PUT
  sends the whole document plus `expectedVersion`; a SignalR `SendEdit`
  sends an insert/delete operation with `baseVersion` (the client diffs
  old vs. new text). One version counter guards both paths, so a stale
  op and a stale PUT fail the same way (409 / `EditRejected`). This
  avoids operational transformation entirely: the version check is the
  conflict detector.
- **SQLite.** Zero external dependencies — the whole app runs with
  `docker compose up --build` and no keys. The trade-off is that SQLite
  does not scale out, so a production deployment moves the connection
  string to a real database (see README deployment notes).
- **In-memory presence.** Presence and cursor state live in SignalR
  group membership, not the database. A restart clears presence but
  never documents.

## Data model (conceptual)

- **Document** — id, title, content, version (integer, +1 per accepted
  edit), created/updated timestamps.
- **Comment** — id, document id, author display name, body, created
  timestamp. Comments are not versioned and are not realtime; clients
  fetch them over REST.

## Scaling notes

- One API instance handles the demo fine. To scale out: put the hub
  behind the SignalR Redis backplane, move SQLite to Azure SQL (or
  another shared store), and serve the web app from Static Web Apps or
  any static host.
