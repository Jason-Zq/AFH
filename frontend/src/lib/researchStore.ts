import { reactive } from 'vue'
import { streamResearch } from './sse'
import { getSession, listSessions } from './api'
import type { ResearchRequest, SessionSummary, SseEvent } from '../types/events'

/** 协作过程面板的 UI 状态模型（对应后端 executor 事件流逐步构建）。 */
export interface TextItem { kind: 'text'; content: string }
export interface ToolItem { kind: 'tool'; name: string; args: string; result: string | null }
export type BlockItem = TextItem | ToolItem
export interface ExecutorBlock { name: string; active: boolean; items: BlockItem[] }

const state = reactive({
  question: '',
  running: false,
  error: null as string | null,
  reportMarkdown: null as string | null,
  blocks: [] as ExecutorBlock[],
  sessions: [] as SessionSummary[],
  switches: { todoList: true, planExecuteMode: true, fileMemory: false, skillDiscovery: true },
})

let abort: AbortController | null = null

function getBlock(executorId: string): ExecutorBlock {
  const last = state.blocks[state.blocks.length - 1]
  if (last && last.name === executorId) return last
  for (const block of state.blocks) block.active = false
  const created: ExecutorBlock = { name: executorId, active: true, items: [] }
  state.blocks.push(created)
  return created
}

function apply(evt: SseEvent): void {
  switch (evt.type) {
    case 'executor-start':
      getBlock(evt.id)
      break
    case 'text': {
      const block = getBlock(evt.executorId)
      const last = block.items[block.items.length - 1]
      if (last?.kind === 'text') last.content += evt.delta
      else block.items.push({ kind: 'text', content: evt.delta })
      break
    }
    case 'tool-call':
      getBlock(evt.executorId).items.push({ kind: 'tool', name: evt.name, args: formatArgs(evt.args), result: null })
      break
    case 'tool-result': {
      const items = getBlock(evt.executorId).items
      const tool = [...items].reverse().find(i => i.kind === 'tool')
      if (tool?.kind === 'tool') tool.result = evt.text
      break
    }
    case 'report':
      if (evt.markdown) state.reportMarkdown = evt.markdown
      break
    case 'error':
      state.error = evt.message
      break
    case 'done':
      break
  }
}

function formatArgs(argsJson: string): string {
  try {
    return Object.entries(JSON.parse(argsJson) as Record<string, unknown>)
      .map(([k, v]) => `${k}=${v}`).join(', ')
  } catch {
    return argsJson
  }
}

async function start(): Promise<void> {
  if (state.running || !state.question.trim()) return
  state.running = true
  state.error = null
  state.reportMarkdown = null
  state.blocks = []
  abort = new AbortController()

  const request: ResearchRequest = { question: state.question.trim(), ...state.switches }
  try {
    await streamResearch(request, apply, abort.signal)
  } catch (ex) {
    state.error = ex instanceof DOMException && ex.name === 'AbortError'
      ? '已取消本次研究。'
      : `运行出错：${ex instanceof Error ? ex.message : String(ex)}`
  } finally {
    state.running = false
    for (const block of state.blocks) block.active = false
    void refreshSessions()
  }
}

function cancel(): void {
  abort?.abort()
}

async function refreshSessions(): Promise<void> {
  try {
    state.sessions = await listSessions()
  } catch {
    // 历史列表加载失败不阻塞主流程
  }
}

/** 从历史记录回放：恢复提问与定稿报告（不重放过程）。 */
async function loadSession(id: number): Promise<void> {
  try {
    const detail = await getSession(id)
    state.question = detail.question
    state.reportMarkdown = detail.reportMarkdown
    state.blocks = []
    state.error = detail.status === 'Completed' ? null : `该次研究未正常完成（${detail.status}）。`
  } catch (ex) {
    state.error = `加载历史会话失败：${ex instanceof Error ? ex.message : String(ex)}`
  }
}

export function useResearch() {
  return { state, start, cancel, refreshSessions, loadSession }
}
