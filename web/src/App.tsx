import React, { useEffect, useState } from 'react';
import { DocumentList } from './components/DocumentList';
import { Editor } from './components/Editor';

const NAME_KEY = 'collabdocs.displayName';

export default function App() {
  const [displayName, setDisplayName] = useState<string>(() => localStorage.getItem(NAME_KEY) ?? '');
  const [nameDraft, setNameDraft] = useState('');
  const [openId, setOpenId] = useState<string | null>(null);

  useEffect(() => {
    if (displayName) localStorage.setItem(NAME_KEY, displayName);
  }, [displayName]);

  const saveName = (e: React.FormEvent) => {
    e.preventDefault();
    if (nameDraft.trim()) setDisplayName(nameDraft.trim());
  };

  return (
    <div className="app">
      <header className="app-header">
        <div>
          <h1>CollabDocs</h1>
          <p className="tagline">Realtime collaborative documents — write together, live.</p>
        </div>
        {displayName && (
          <form
            className="name-form"
            onSubmit={(e) => {
              e.preventDefault();
              if (nameDraft.trim()) {
                setDisplayName(nameDraft.trim());
                setNameDraft('');
              }
            }}
          >
            <label>
              Display name
              <input
                value={nameDraft}
                onChange={(e) => setNameDraft(e.target.value)}
                placeholder={displayName}
                aria-label="Display name"
              />
            </label>
            <button type="submit" disabled={!nameDraft.trim()}>Change</button>
          </form>
        )}
      </header>

      {!displayName ? (
        <div className="name-prompt">
          <h2>What should collaborators call you?</h2>
          <p className="muted">Your display name shows up in the presence bar for everyone editing with you.</p>
          <form onSubmit={saveName}>
            <input
              value={nameDraft}
              onChange={(e) => setNameDraft(e.target.value)}
              placeholder="e.g. Varun Kumar"
              aria-label="Display name"
              autoFocus
            />
            <button type="submit" disabled={!nameDraft.trim()}>Start collaborating</button>
          </form>
        </div>
      ) : openId ? (
        <Editor documentId={openId} displayName={displayName} onBack={() => setOpenId(null)} />
      ) : (
        <DocumentList onOpen={setOpenId} />
      )}

      <footer className="app-footer">
        Demo workspace — no sign-in required, so anyone with this link can read and edit these documents.
        Realtime sync runs over SignalR; documents are stored by the CollabDocs API.
      </footer>
    </div>
  );
}
