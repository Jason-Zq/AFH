import type { SessionDetail, SessionSummary, StatusInfo } from '../types/events'

/** 普通 JSON 端点统一走这里（流式端点见 sse.ts）。 */

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`请求失败：HTTP ${response.status}`)
  }
  return response.json() as Promise<T>
}

export const getStatus = () => getJson<StatusInfo>('/api/status')

export const listSessions = () => getJson<SessionSummary[]>('/api/sessions')

export const getSession = (id: number) => getJson<SessionDetail>(`/api/sessions/${id}`)
