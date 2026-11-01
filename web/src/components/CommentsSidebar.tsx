import React, { useEffect, useState } from 'react';
import { createComment, deleteComment, listComments } from '../lib/api';
import type { CommentItem } from '../lib/types';

export function CommentsSidebar({
  documentId,
  displayName,
  caretPosition,
}: {
  documentId: string;
  displayName: string;
  caretPosition: number;
}) {
  const [comments, setComments] = useState<CommentItem[]>([]);
  const [text, setText] = useState('');
  const [author, setAuthor] = useState(displayName);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setAuthor(displayName);
  }, [displayName]);

  useEffect(() => {
    listComments(documentId)
      .then(setComments)
      .catch((e) => setError(String(e?.message ?? e)));
  }, [documentId]);

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!text.trim() || !author.trim()) return;
    try {
      const c = await createComment(documentId, author.trim(), text.trim(), caretPosition);
      setComments((prev) => [...prev, c]);
      setText('');
      setError(null);
    } catch (err) {
      setError(String((err as Error)?.message ?? err));
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteComment(documentId, id);
      setComments((prev) => prev.filter((c) => c.id !== id));
    } catch (err) {
      setError(`Could not delete comment: ${String((err as Error)?.message ?? err)}`);
    }
  };

  return (
    <aside className="comments-sidebar">
      <h3>Comments ({comments.length})</h3>
      {error && <p className="error">{error}</p>}
      <ul className="comment-list">
        {comments.map((c) => (
          <li key={c.id} className="comment">
            <div className="comment-head">
              <strong>{c.author}</strong>
              <span className="muted small">
                {new Date(c.createdAtUtc).toLocaleString()}
                {c.anchorPosition > 0 ? ` · @${c.anchorPosition}` : ''}
              </span>
            </div>
            <p>{c.text}</p>
            <button className="text-btn" onClick={() => handleDelete(c.id)}>
              Delete
            </button>
          </li>
        ))}
        {comments.length === 0 && <li className="muted">No comments yet.</li>}
      </ul>
      <form className="comment-form" onSubmit={handleAdd}>
        <input value={author} onChange={(e) => setAuthor(e.target.value)} placeholder="Your name" aria-label="Comment author" />
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Add a comment… (anchored at your current caret position)"
          rows={3}
          aria-label="Comment text"
        />
        <button type="submit" disabled={!text.trim() || !author.trim()}>
          Add comment
        </button>
      </form>
    </aside>
  );
}
