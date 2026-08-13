// 与后端 SSE 契约一一对应（ResearchController 的 WriteSseAsync 载荷）。
// 改这里必须先改后端——协议是双端镜像，编译器帮你抓不同步。

export interface ResearchRequest {
  question: string
  todoList: boolean
  planExecuteMode: boolean
  fileMemory: boolean
  skillDiscovery: boolean
}

export type SseEvent =
  | { type: 'executor-start'; id: string }
  | { type: 'text'; executorId: string; delta: string }
  | { type: 'tool-call'; executorId: string; name: string; args: string }
  | { type: 'tool-result'; executorId: string; text: string }
  | { type: 'report'; markdown: string | null }
  | { type: 'error'; message: string }
  | { type: 'done' }

export interface SessionSummary {
  id: number
  question: string
  status: string
  createdAt: string
}

export interface SessionDetail extends SessionSummary {
  reportMarkdown: string | null
  switches: string
}

export interface StatusInfo {
  corpusChunkCount: number
}
