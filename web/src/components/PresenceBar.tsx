import React from 'react';

const COLORS = ['#2563eb', '#059669', '#d97706', '#7c3aed', '#db2777', '#0891b2', '#65a30d', '#c2410c'];

export function colorFor(name: string): string {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) >>> 0;
  return COLORS[h % COLORS.length];
}

export function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .map((p) => p[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

export function PresenceBar({
  presence,
  selfName,
  cursors,
  connected,
}: {
  presence: string[];
  selfName: string;
  cursors: Record<string, number>;
  connected: boolean;
}) {
  const names = presence.includes(selfName) ? presence : [selfName, ...presence];
  const remoteCursors = Object.entries(cursors).filter(([n]) => n !== selfName);
  return (
    <div className="presence-bar">
      <span className={`conn-dot ${connected ? 'on' : 'off'}`} title={connected ? 'Connected' : 'Disconnected'} />
      <span className="presence-label">{connected ? 'Live' : 'Connecting…'}:</span>
      {names.map((n) => (
        <span key={n} className="avatar-chip" style={{ background: colorFor(n) }} title={n}>
          {initials(n)}
          <span className="avatar-name">{n}</span>
        </span>
      ))}
      {remoteCursors.length > 0 && (
        <span className="cursor-notes">
          {remoteCursors.map(([n, pos]) => (
            <span key={n} className="cursor-note">
              {n} is editing near character {pos}
            </span>
          ))}
        </span>
      )}
    </div>
  );
}
