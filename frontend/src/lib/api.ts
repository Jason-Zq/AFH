import type { SessionDetail, SessionSummary, StatusInfo } from '../types/events'
import type {
  AdminSessionSummary,
  DocumentDetail,
  DocumentInput,
  DocumentSummary,
  LogEntry,
  PagedResult,
  RebuildResult,
  ReseedResult,
  SeedMode,
  SystemStatus,
} from '../types/admin'

/** 普通 JSON 端点统一走这里（流式端点见 sse.ts）。 */

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`请求失败：HTTP ${response.status}`)
  }
  return response.json() as Promise<T>
}

/** 带请求体的端点；非 2xx 时透传后端 { message } 友好文案（如 409 重名）。 */
async function sendJson<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    let message = `请求失败：HTTP ${response.status}`
    try {
      const data = (await response.json()) as { message?: string }
      if (data.message) {
        message = data.message
      }
    } catch {
      // 非 JSON 错误体（如 [ApiController] 自动校验的 ProblemDetails），用默认文案
    }
    throw new Error(message)
  }
  if (response.status === 204) {
    return undefined as T
  }
  return response.json() as Promise<T>
}

export const getStatus = () => getJson<StatusInfo>('/api/status')

export const listSessions = () => getJson<SessionSummary[]>('/api/sessions')

export const getSession = (id: number) => getJson<SessionDetail>(`/api/sessions/${id}`)

// ===== 后台管理 =====

export const listAdminDocuments = (page: number, pageSize: number, search: string) =>
  getJson<PagedResult<DocumentSummary>>(
    `/api/admin/documents?page=${page}&pageSize=${pageSize}&search=${encodeURIComponent(search)}`,
  )

export const getAdminDocument = (id: number) => getJson<DocumentDetail>(`/api/admin/documents/${id}`)

// 写操作返回的 corpusChunkCount 可能为 null：写已落库但自动重建失败（前端据此提示去运维页手动重建）
export const createDocument = (input: DocumentInput) =>
  sendJson<{ id: number; corpusChunkCount: number | null }>('POST', '/api/admin/documents', input)

export const updateDocument = (id: number, input: DocumentInput) =>
  sendJson<{ id: number; corpusChunkCount: number | null }>('PUT', `/api/admin/documents/${id}`, input)

export const deleteDocument = (id: number) => sendJson<void>('DELETE', `/api/admin/documents/${id}`)

export const listAdminSessions = (page: number, pageSize: number, status: string) =>
  getJson<PagedResult<AdminSessionSummary>>(
    `/api/admin/sessions?page=${page}&pageSize=${pageSize}&status=${encodeURIComponent(status)}`,
  )

export const deleteSession = (id: number) => sendJson<void>('DELETE', `/api/admin/sessions/${id}`)

export const deleteSessions = (ids: number[]) =>
  sendJson<{ deleted: number }>('POST', '/api/admin/sessions/delete-batch', { ids })

export const getSystemStatus = () => getJson<SystemStatus>('/api/admin/ops/status')

export const rebuildIndex = () => sendJson<RebuildResult>('POST', '/api/admin/ops/rebuild-index')

export const reseedCorpus = (mode: SeedMode) =>
  sendJson<ReseedResult>('POST', '/api/admin/ops/reseed-corpus', { mode })

export const getLogs = (minLevel: string, take: number) =>
  getJson<LogEntry[]>(`/api/admin/ops/logs?minLevel=${encodeURIComponent(minLevel)}&take=${take}`)
