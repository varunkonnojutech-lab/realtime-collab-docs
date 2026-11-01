import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { HUB_URL } from './api';
import type { EditAppliedPayload, EditRejectedPayload } from './types';

export interface CollabState {
  connected: boolean;
  presence: string[];
  cursors: Record<string, number>;
  connectionError: string | null;
}

/**
 * Owns the SignalR connection for one open document. The caller supplies
 * callbacks for server-driven content changes so editor state stays in the
 * editor component; this hook only manages connection + presence + cursors.
 */
export function useCollab(
  documentId: string | null,
  displayName: string,
  onEditApplied: (payload: EditAppliedPayload) => void,
  onEditRejected: (payload: EditRejectedPayload) => void,
) {
  const [state, setState] = useState<CollabState>({
    connected: false,
    presence: [],
    cursors: {},
    connectionError: null,
  });
  const connRef = useRef<signalR.HubConnection | null>(null);
  const callbacksRef = useRef({ onEditApplied, onEditRejected });
  callbacksRef.current = { onEditApplied, onEditRejected };

  useEffect(() => {
    if (!documentId || !displayName) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build();
    connRef.current = conn;

    conn.on('EditApplied', (p: EditAppliedPayload) => callbacksRef.current.onEditApplied(p));
    conn.on('EditRejected', (p: EditRejectedPayload) => callbacksRef.current.onEditRejected(p));
    conn.on('PresenceList', (names: string[]) => setState((s) => ({ ...s, presence: names })));
    conn.on('UserJoined', (p: { displayName: string }) =>
      setState((s) => ({ ...s, presence: s.presence.includes(p.displayName) ? s.presence : [...s.presence, p.displayName] })),
    );
    conn.on('UserLeft', (p: { displayName: string }) =>
      setState((s) => ({
        ...s,
        presence: s.presence.filter((n) => n !== p.displayName),
        cursors: Object.fromEntries(Object.entries(s.cursors).filter(([k]) => k !== p.displayName)),
      })),
    );
    conn.on('CursorMoved', (p: { displayName: string; position: number }) =>
      setState((s) => ({ ...s, cursors: { ...s.cursors, [p.displayName]: p.position } })),
    );

    conn
      .start()
      .then(() => {
        setState((s) => ({ ...s, connected: true, connectionError: null }));
        return conn.invoke('JoinDocument', documentId, displayName);
      })
      .catch((err) => setState((s) => ({ ...s, connectionError: String(err?.message ?? err) })));

    return () => {
      conn
        .invoke('LeaveDocument', documentId)
        .catch(() => undefined)
        .finally(() => {
          conn.stop().catch(() => undefined);
        });
      connRef.current = null;
    };
  }, [documentId, displayName]);

  const sendEdit = async (edit: {
    type: 'insert' | 'delete';
    position: number;
    text?: string;
    length?: number;
    baseVersion: number;
  }) => {
    const conn = connRef.current;
    if (!conn || !documentId) return;
    await conn.invoke('SendEdit', documentId, edit);
  };

  const sendCursor = (position: number) => {
    const conn = connRef.current;
    if (!conn || !documentId) return;
    conn.invoke('CursorMove', documentId, position).catch(() => undefined);
  };

  return { ...state, sendEdit, sendCursor };
}
