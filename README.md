# Realtime Collab Docs

[![CI](https://github.com/varunkonnojutech-lab/realtime-collab-docs/actions/workflows/ci.yml/badge.svg)](https://github.com/varunkonnojutech-lab/realtime-collab-docs/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![React 18](https://img.shields.io/badge/React-18-61DAFB.svg)](https://react.dev/)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/varunkonnojutech-lab/realtime-collab-docs)

**A Notion-like collaborative document editor where several people can edit the same document at the same time and see each other's changes live.**

## Features

- **Document list** — create, open, rename, and delete documents from a sidebar.
- **Live co-editing** — edits broadcast to everyone in the document over SignalR the moment they are saved; no refresh needed.
- **Live cursors and presence** — see who else is in the document and where their cursor is, in real time.
- **Versioned saves with conflict protection** — every save carries the version it was based on; a stale save is rejected with HTTP 409 instead of silently overwriting someone else's work.
- **Comments** — anchored comments on a document via a REST endpoint (author, text, anchor position), visible to everyone in the document.
- **One-command run** — `docker compose up --build` starts the API and the web app together.
- **Runs anywhere** — SQLite storage, no external services, no keys to configure.

## Architecture

```mermaid
flowchart LR
    subgraph Browser
        UI[React 18 + Vite + TypeScript<br/>@microsoft/signalr client]
    end
    subgraph API["ASP.NET Core 8 API"]
        REST[REST controllers<br/>/api/documents, /api/documents/{id}/comments]
        HUB[SignalR hub<br/>/hubs/docs]
        DB[(EF Core + SQLite<br/>collabdocs.db)]
    end
    UI -- "REST (CRUD, comments)" --> REST
    UI -- "SignalR (edits, cursors, presence)" --> HUB
    REST --> DB
    HUB --> DB
```

- **Backend:** .NET 8 ASP.NET Core API + SignalR + Entity Framework Core (SQLite).
- **Frontend:** React 18 + Vite + TypeScript, using the `@microsoft/signalr` client.
- **Realtime model:** the document is the unit of collaboration. A client joins a SignalR group per document; edits, cursor moves, and presence flow through that group. REST remains the source of truth for document CRUD and comments — see [docs/realtime-protocol.md](docs/realtime-protocol.md).

## Quick start

### Docker (recommended)

```bash
docker compose up --build
```

- Editor UI: <http://localhost:3000>
- API: <http://localhost:8080> (health check: <http://localhost:8080/health>)

Documents persist in a Docker named volume (`api-data`) holding the SQLite file, so your documents survive container restarts.

### Local development

Terminal 1 — API:

```bash
dotnet run --project src/CollabDocs.Api
# API on http://localhost:8080
```

Terminal 2 — web app:

```bash
cd web
npm install
npm run dev
# Vite dev server on http://localhost:5173
```

The Vite dev server proxies `/api` and `/hubs` to the API on port 8080, so the frontend works unchanged in both setups.

## API overview

### REST

| Method | Endpoint | Description |
| ------ | -------- | ----------- |
| `GET` | `/api/documents` | List documents |
| `POST` | `/api/documents` | Create a document |
| `GET` | `/api/documents/{id}` | Get one document |
| `PUT` | `/api/documents/{id}` | Update a document — send `expectedVersion`; a stale version returns **409 Conflict** |
| `DELETE` | `/api/documents/{id}` | Delete a document |
| `GET` | `/api/documents/{id}/comments` | List comments on a document |
| `POST` | `/api/documents/{id}/comments` | Add a comment |
| `GET` | `/health` | Health check |

### SignalR hub — `/hubs/docs`

Client methods: `JoinDocument`, `LeaveDocument`, `SendEdit`, `CursorMove`.
Server events: `EditApplied`, `EditRejected`, `PresenceList`, `UserJoined`, `UserLeft`, `CursorMoved`.

Full details — edit operation shape, version/conflict behavior, and the presence model — are in [docs/realtime-protocol.md](docs/realtime-protocol.md).

## Project structure

```
realtime-collab-docs/
├── src/
│   └── CollabDocs.Api/        # ASP.NET Core API, SignalR hub, EF Core models
├── tests/
│   └── CollabDocs.Tests/      # xUnit tests
├── web/                       # React 18 + Vite + TypeScript frontend
│   └── src/
├── docs/
│   ├── architecture.md
│   └── realtime-protocol.md
├── .devcontainer/
├── .github/workflows/ci.yml
├── docker-compose.yml
└── CollabDocs.sln
```

## Configuration

| Variable | Default | Description |
| -------- | ------- | ----------- |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment |
| `ASPNETCORE_URLS` | `http://+:8080` | Address the API listens on |
| `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/collabdocs.db` (Docker) | SQLite connection string for the document store — this key matches `src/CollabDocs.Api/appsettings.json` (`DefaultConnection`, default `Data Source=collabdocs.db` locally) |

> Note: the connection-string key above matches `docker-compose.yml`.

## Deployment notes

A brief sketch, not a production recipe:

- **API → Azure App Service:** publish the API (`dotnet publish -c Release`), deploy to an App Service running .NET 8, and set `ASPNETCORE_ENVIRONMENT=Production` plus a `ConnectionStrings__CollabDocs` value pointing at durable storage. Note that SQLite on App Service local disk does not survive slot swaps or scale-out — for a real deployment, move the connection string to Azure SQL or another durable store.
- **Web → Azure Static Web Apps:** build the frontend (`npm run build`) and deploy the `dist/` output; point the app's API base URL at the App Service URL.
- **SignalR:** the in-process hub works for a single API instance. If you scale the API out, add the SignalR Redis backplane so instances share messages.

## Screenshots

Screenshots come from running the app — no placeholder images are shipped. To capture them, run `docker compose up --build` and photograph:

1. **Document list** — the sidebar with document titles and the "New document" action.
2. **Editor** — a document open for editing, showing the title and body fields.
3. **Presence** — two browser windows on the same document, showing the collaborator list and live cursors.
4. **Comments** — the comments panel on a document with a posted comment.

## Security note

**There is no authentication in this project by design.** Anyone who can reach the API or the web app can read and edit every document. This is a demo workspace for learning realtime collaboration, not a multi-tenant product — do not deploy it publicly with real or sensitive documents, and do not claim otherwise. Adding auth (e.g. JWT + per-document access control) is the natural next step before any real use.

## Contributing

Issues and pull requests are welcome. Please keep changes honest and minimal: no invented features, and keep the README in sync with what the code actually does.

## License

MIT — see [LICENSE](LICENSE).
