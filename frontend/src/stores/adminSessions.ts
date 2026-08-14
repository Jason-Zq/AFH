import { defineStore } from 'pinia'
import { ref } from 'vue'
import { deleteSession, deleteSessions, listAdminSessions } from '../lib/api'
import { useSessionsStore } from './sessions'
import type { AdminSessionSummary } from '../types/admin'

/** 后台会话管理：分页列表 + 单个/批量删除。删除后联动刷新前台历史列表。 */
export const useAdminSessionsStore = defineStore('admin-sessions', () => {
  const items = ref<AdminSessionSummary[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const status = ref('')
  const loading = ref(false)

  async function refresh(): Promise<void> {
    loading.value = true
    try {
      const data = await listAdminSessions(page.value, pageSize.value, status.value)
      items.value = data.items
      total.value = data.total
    } catch {
      // 拉取失败保留旧数据（fire-and-forget 调用不产生未处理拒绝）
    } finally {
      loading.value = false
    }
  }

  async function removeOne(id: number): Promise<void> {
    await deleteSession(id)
    await afterMutation()
  }

  /** 返回实际删除条数，供组件提示。 */
  async function removeBatch(ids: number[]): Promise<number> {
    const { deleted } = await deleteSessions(ids)
    await afterMutation()
    return deleted
  }

  async function afterMutation(): Promise<void> {
    await Promise.all([refresh(), useSessionsStore().refresh()])
  }

  return { items, total, page, pageSize, status, loading, refresh, removeOne, removeBatch }
})
