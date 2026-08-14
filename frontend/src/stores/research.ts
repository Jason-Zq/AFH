import { defineStore } from 'pinia'
import { reactive, ref } from 'vue'
import { streamResearch } from '../lib/sse'
import { useSessionsStore } from './sessions'
import type { ResearchRequest, SseEvent } from '../types/events'

/** 协作过程面板的 UI 状态模型（对应后端 executor 事件流逐步构建）。 */
export interface TextItem { kind: 'text'; content: string }
export interface ToolItem { kind: 'tool'; name: string; args: string; result: string | null }
export type BlockItem = TextItem | ToolItem
export interface ExecutorBlock { name: string; active: boolean; startedAt: number; endedAt: number | null; items: BlockItem[] }

function formatArgs(argsJson: string): string {
  try {
    return Object.entries(JSON.parse(argsJson) as Record<string, unknown>)
      .map(([k, v]) => `${k}=${v}`).join(', ')
  } catch {
    return argsJson
  }
}

/** 一次研究的运行时状态：SSE 事件流 → blocks 状态机。 */
export const useResearchStore = defineStore('research', () => {
  const question = ref('')
  const running = ref(false)
  const error = ref<string | null>(null)
  const notice = ref<string | null>(null) // 非错误提示（如"已取消"）
  const reportMarkdown = ref<string | null>(null)
  const blocks = reactive<ExecutorBlock[]>([])
  const switches = reactive({ todoList: true, planExecuteMode: true, fileMemory: false, skillDiscovery: true })

  let abort: AbortController | null = null

  function getBlock(executorId: string): ExecutorBlock {
    const last = blocks[blocks.length - 1]
    if (last && last.name === executorId) return last
    const now = Date.now()
    for (const block of blocks) {
      block.active = false
      block.endedAt ??= now
    }
    const created: ExecutorBlock = { name: executorId, active: true, startedAt: now, endedAt: null, items: [] }
    blocks.push(created)
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
        if (evt.markdown) reportMarkdown.value = evt.markdown
        break
      case 'error':
        error.value = evt.message
        break
      case 'done':
        break
    }
  }

  async function start(): Promise<void> {
    if (running.value || !question.value.trim()) return
    running.value = true
    error.value = null
    notice.value = null
    reportMarkdown.value = null
    blocks.splice(0)
    abort = new AbortController()

    const request: ResearchRequest = { question: question.value.trim(), ...switches }
    try {
      await streamResearch(request, apply, abort.signal)
    } catch (ex) {
      if (ex instanceof DOMException && ex.name === 'AbortError') {
        notice.value = '已取消本次研究。'
      } else {
        error.value = `运行出错：${ex instanceof Error ? ex.message : String(ex)}`
      }
    } finally {
      running.value = false
      const now = Date.now()
      for (const block of blocks) {
        block.active = false
        block.endedAt ??= now
      }
      void useSessionsStore().refresh()
    }
  }

  function cancel(): void {
    abort?.abort()
  }

  return { question, running, error, notice, reportMarkdown, blocks, switches, start, cancel }
})
