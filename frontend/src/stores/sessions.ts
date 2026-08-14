import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listSessions } from '../lib/api'
import type { SessionSummary } from '../types/events'

/** 历史会话列表（侧栏）。 */
export const useSessionsStore = defineStore('sessions', () => {
  const sessions = ref<SessionSummary[]>([])

  async function refresh(): Promise<void> {
    try {
      sessions.value = await listSessions()
    } catch {
      // 历史列表加载失败不阻塞主流程
    }
  }

  return { sessions, refresh }
})
