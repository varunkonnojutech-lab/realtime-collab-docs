import React, { useCallback, useEffect, useRef, useState } from 'react';
import { diffToEdit, getDocument, updateDocument } from '../lib/api';
import type { EditAppliedPayload, EditRejectedPayload } from '../lib/types';
import { useCollab } from '../lib/useCollab';
import { renderMarkdown } from '../lib/markdown';
import { PresenceBar } from './PresenceBar';
import { CommentsSidebar } from './CommentsSidebar';

const DEBOUNCE_MS = 400;

export function Editor({
  documentId,
  displayName,
  onBack,
}: {
  documentId: string;
  displayName: string;
  onBack: () => void;
}) {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [preview, setPreview] = useState(false);
  const [savingTitle, setSavingTitle] = useState(false);
  const [caret, setCaret] = useState(0);

  const contentRef = useRef('');
  const lastSyncedRef = useRef('');
  const versionRef = useRef(0);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastCursorSentRef = useRef(0);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);

  const setContentBoth = (v: string) => {
    contentRef.current = v;
    setContent(v);
  };

  const onEditApplied = useCallback((p: EditAppliedPayload) => {
    versionRef.current = p.version;
    const local = contentRef.current;
    const prevSynced = lastSyncedRef.current;
    lastSyncedRef.current = p.content;
    if (p.content === local) {
      // Echo of our own edit (or identical content): nothing to replace.
      return;
    }
    if (local === prevSynced) {
      // No unsent local edits: adopt the remote content.
      setContentBoth(p.content);
    }
    // Else: keep local text; the next debounced send reconciles the diff
    // between local text and the new server content at the new base version.
  }, []);

  const onEditRejected = useCallback((p: EditRejectedPayload) => {
    versionRef.current = p.currentVersion;
    lastSyncedRef.current = p.content;
    setNotice('Synced to latest version — your pending edits will be re-sent.');
    // Keep local text: the next debounced send diffs it against the new
    // server content, so no typing is lost on a version conflict.
  }, []);

  const { connected, presence, cursors, connectionError, sendEdit, sendCursor } = useCollab(
    documentId,
    displayName,
    onEditApplied,
    onEditRejected,
  );

  useEffect(() => {
    setLoading(true);
    getDocument(documentId)
      .then((d) => {
        setTitle(d.title);
        setContentBoth(d.content ?? '');
        lastSyncedRef.current = d.content ?? '';
        versionRef.current = d.version;
      })
      .catch((e) => setError(String(e?.message ?? e)))
      .finally(() => setLoading(false));
  }, [documentId]);

  // Debounced send: one SendEdit diff vs last-synced content per tick.
  // A replace (delete + insert) is sent as delete first; the insert follows
  // on the next tick once the delete is reflected in lastSynced.
  const scheduleSend = useCallback(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      const local = contentRef.current;
      const synced = lastSyncedRef.current;
      const diff = diffToEdit(synced, local);
      if (!diff || (!diff.deleted && !diff.inserted)) return;
      const baseVersion = versionRef.current;
      if (diff.deleted) {
        sendEdit({
          type: 'delete',
          position: diff.deleted.position,
          length: diff.deleted.length,
          baseVersion,
        }).catch((e) => setError(String(e?.message ?? e)));
        // Optimistic: lastSynced becomes the post-delete text; a pending
        // insert (if any) is diffed and sent on the next tick.
        lastSyncedRef.current =
          synced.slice(0, diff.deleted.position) +
          synced.slice(diff.deleted.position + diff.deleted.length);
      } else if (diff.inserted) {
        sendEdit({
          type: 'insert',
          position: diff.inserted.position,
          text: diff.inserted.text,
          baseVersion,
        }).catch((e) => setError(String(e?.message ?? e)));
        lastSyncedRef.current = local;
      }
    }, DEBOUNCE_MS);
  }, [sendEdit]);

  useEffect(() => () => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  const handleContentChange = (v: string) => {
    setContentBoth(v);
    setNotice(null);
    scheduleSend();
  };

  const handleCursor = () => {
    const pos = textareaRef.current?.selectionStart ?? 0;
    setCaret(pos);
    const now = Date.now();
    if (now - lastCursorSentRef.current > 500) {
      lastCursorSentRef.current = now;
      sendCursor(pos);
    }
  };

  const saveTitle = async () => {
    setSavingTitle(true);
    try {
      const updated = await updateDocument(documentId, title, contentRef.current, versionRef.current);
      versionRef.current = updated.version;
      setError(null);
    } catch (e) {
      setError(`Could not save title: ${String((e as Error)?.message ?? e)}`);
    } finally {
      setSavingTitle(false);
    }
  };

  if (loading) return <p className="muted">Loading document…</p>;

  return (
    <div className="editor-page">
      <div className="editor-topbar">
        <button className="text-btn" onClick={onBack}>← All documents</button>
        <PresenceBar presence={presence} selfName={displayName} cursors={cursors} connected={connected} />
      </div>
      {connectionError && <p className="error">Realtime connection failed: {connectionError} (editing still saves via debounced edits when reconnected).</p>}
      {error && <p className="error">{error}</p>}
      {notice && <p className="notice">{notice}</p>}
      <div className="title-row">
        <input
          className="title-input"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          onBlur={saveTitle}
          placeholder="Untitled document"
          aria-label="Document title"
        />
        <button onClick={saveTitle} disabled={savingTitle}>
          {savingTitle ? 'Saving…' : 'Save title'}
        </button>
        <button className={preview ? 'active-btn' : ''} onClick={() => setPreview((p) => !p)}>
          {preview ? 'Edit' : 'Markdown preview'}
        </button>
      </div>
      <div className="editor-layout">
        <div className="editor-main">
          {preview ? (
            <div className="markdown-preview" dangerouslySetInnerHTML={{ __html: renderMarkdown(content) }} />
          ) : (
            <textarea
              ref={textareaRef}
              className="editor-textarea"
              value={content}
              onChange={(e) => handleContentChange(e.target.value)}
              onKeyUp={handleCursor}
              onClick={handleCursor}
              onSelect={handleCursor}
              placeholder="Start writing… Markdown supported (# headings, **bold**, lists, `code`)."
              aria-label="Document content"
            />
          )}
          <p className="muted small">Edits sync automatically as you type. Version {versionRef.current} · caret at character {caret}</p>
        </div>
        <CommentsSidebar documentId={documentId} displayName={displayName} caretPosition={caret} />
      </div>
    </div>
  );
}
