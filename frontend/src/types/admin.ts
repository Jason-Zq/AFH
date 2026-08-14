/** 后台管理 API 类型（REST 接口，与 SSE 事件协议无关）。 */

export interface PagedResult<T> {
  total: number
  items: T[]
}

export interface DocumentSummary {
  id: number
  name: string
  contentPreview: string
  contentLength: number
  updatedAt: string
}

export interface DocumentDetail {
  id: number
  name: string
  content: string
  updatedAt: string
}

export interface DocumentInput {
  name: string
  content: string
}

export interface AdminSessionSummary {
  id: number
  question: string
  status: string
  createdAt: string
  reportLength: number
}

export interface SystemStatus {
  dbConnected: boolean
  documentCount: number
  sessionCount: number
  appliedMigrations: string[]
  corpusChunkCount: number
  uptimeSeconds: number
  isResearchRunning: boolean
  corpusDirectory: string | null
}

export interface LogEntry {
  timestamp: string
  level: string
  category: string
  message: string
  exception: string | null
}

export type SeedMode = 'append' | 'replace'

export interface RebuildResult {
  chunkCount: number
  elapsedMs: number
}

export interface ReseedResult {
  imported: number
  skipped: number
  deleted: number
  chunkCount: number
}
