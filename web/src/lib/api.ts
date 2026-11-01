import type { CommentItem, DocumentDetail, DocumentSummary } from './types';

/**
 * API base resolution:
 * - VITE_API_URL set (e.g. http://localhost:8080) -> use it verbatim.
 * - VITE_API_URL set to "" -> relative "/api" (Vite dev proxy / nginx proxy).
 * - VITE_API_URL unset -> default http://localhost:8080 (per backend contract).
 * The Vite dev server also proxies /api and /hubs to localhost:8080, so
 * setting VITE_API_URL="" is the CORS-free dev path; the Docker/nginx build
 * uses the same relative path and proxies to http://api:8080.
 */
const raw = import.meta.env.VITE_API_URL as string | undefined;
export const API_BASE = raw === undefined ? 'http://localhost:8080' : raw.replace(/\/$/, '');
export const HUB_URL = `${API_BASE}/hubs/docs`;

const base = `${API_BASE}/api/documents`;

async function req<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`API ${res.status}: ${text || res.statusText}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export async function listDocuments(): Promise<DocumentSummary[]> {
  return req(await fetch(base));
}

export async function createDocument(title: string, content = ''): Promise<DocumentDetail> {
  return req(
    await fetch(base, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content }),
    }),
  );
}

export async function getDocument(id: string): Promise<DocumentDetail> {
  return req(await fetch(`${base}/${encodeURIComponent(id)}`));
}

export async function updateDocument(
  id: string,
  title: string,
  content: string,
  expectedVersion: number,
): Promise<DocumentDetail> {
  return req(
    await fetch(`${base}/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, content, expectedVersion }),
    }),
  );
}

export async function deleteDocument(id: string): Promise<void> {
  await req(await fetch(`${base}/${encodeURIComponent(id)}`, { method: 'DELETE' }));
}

export async function listComments(documentId: string): Promise<CommentItem[]> {
  return req(await fetch(`${base}/${encodeURIComponent(documentId)}/comments`));
}

export async function createComment(
  documentId: string,
  author: string,
  text: string,
  anchorPosition = 0,
): Promise<CommentItem> {
  return req(
    await fetch(`${base}/${encodeURIComponent(documentId)}/comments`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ author, text, anchorPosition }),
    }),
  );
}

/**
 * NOTE: the backend contract supplied to the frontend lists GET/POST for
 * comments only. Delete attempts DELETE /documents/{id}/comments/{commentId};
 * if the backend does not implement it, the UI keeps the comment and shows
 * the server error instead of pretending it was deleted.
 */
export async function deleteComment(documentId: string, commentId: string): Promise<void> {
  await req(
    await fetch(
      `${base}/${encodeURIComponent(documentId)}/comments/${encodeURIComponent(commentId)}`,
      { method: 'DELETE' },
    ),
  );
}

/** Common-prefix / common-suffix diff. Returns null when texts are equal. */
export function diffToEdit(
  oldText: string,
  newText: string,
): { deleted: { position: number; length: number } | null; inserted: { position: number; text: string } | null } | null {
  if (oldText === newText) return null;
  let prefix = 0;
  while (
    prefix < oldText.length &&
    prefix < newText.length &&
    oldText[prefix] === newText[prefix]
  ) {
    prefix++;
  }
  let suffix = 0;
  while (
    suffix < oldText.length - prefix &&
    suffix < newText.length - prefix &&
    oldText[oldText.length - 1 - suffix] === newText[newText.length - 1 - suffix]
  ) {
    suffix++;
  }
  const deletedText = oldText.slice(prefix, oldText.length - suffix);
  const insertedText = newText.slice(prefix, newText.length - suffix);
  return {
    deleted: deletedText.length > 0 ? { position: prefix, length: deletedText.length } : null,
    inserted: insertedText.length > 0 ? { position: prefix, text: insertedText } : null,
  };
}
