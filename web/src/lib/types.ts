export interface DocumentSummary {
  id: string;
  title: string;
  updatedAtUtc: string;
  version: number;
  preview: string;
}

export interface DocumentDetail {
  id: string;
  title: string;
  content: string;
  updatedAtUtc: string;
  version: number;
}

export interface CommentItem {
  id: string;
  documentId: string;
  author: string;
  text: string;
  anchorPosition: number;
  createdAtUtc: string;
}

export type EditOp =
  | { type: 'insert'; position: number; text: string; baseVersion: number }
  | { type: 'delete'; position: number; length: number; baseVersion: number };

export interface EditAppliedPayload {
  documentId: string;
  edit: unknown;
  version: number;
  content: string;
}

export interface EditRejectedPayload {
  documentId: string;
  currentVersion: number;
  content: string;
}
