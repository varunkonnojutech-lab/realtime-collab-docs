import React, { useEffect, useState } from 'react';
import { createDocument, deleteDocument, listDocuments } from '../lib/api';
import type { DocumentSummary } from '../lib/types';

export function DocumentList({ onOpen }: { onOpen: (id: string) => void }) {
  const [docs, setDocs] = useState<DocumentSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [title, setTitle] = useState('');
  const [creating, setCreating] = useState(false);

  const refresh = () => {
    setLoading(true);
    listDocuments()
      .then(setDocs)
      .catch((e) => setError(String(e?.message ?? e)))
      .finally(() => setLoading(false));
  };

  useEffect(refresh, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const t = title.trim();
    if (!t) return;
    setCreating(true);
    try {
      const doc = await createDocument(t);
      setTitle('');
      onOpen(doc.id);
    } catch (err) {
      setError(String((err as Error)?.message ?? err));
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id: string, docTitle: string) => {
    if (!window.confirm(`Delete "${docTitle}"? This cannot be undone.`)) return;
    try {
      await deleteDocument(id);
      setDocs((d) => d.filter((x) => x.id !== id));
    } catch (err) {
      setError(String((err as Error)?.message ?? err));
    }
  };

  return (
    <div className="list-page">
      <h2>Your documents</h2>
      <form className="create-form" onSubmit={handleCreate}>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="New document title…"
          aria-label="New document title"
        />
        <button type="submit" disabled={creating || !title.trim()}>
          {creating ? 'Creating…' : 'New document'}
        </button>
      </form>
      {error && <p className="error">{error}</p>}
      {loading ? (
        <p className="muted">Loading documents…</p>
      ) : docs.length === 0 ? (
        <p className="muted">No documents yet — create one above to start collaborating.</p>
      ) : (
        <ul className="doc-list">
          {docs.map((d) => (
            <li key={d.id} className="doc-row">
              <button className="doc-open" onClick={() => onOpen(d.id)}>
                <span className="doc-title">{d.title}</span>
                <span className="doc-meta">
                  v{d.version} · {new Date(d.updatedAtUtc).toLocaleString()}
                </span>
                {d.preview && <span className="doc-preview">{d.preview}</span>}
              </button>
              <button className="danger-btn" onClick={() => handleDelete(d.id, d.title)}>
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
