import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getLogs, getSystemStatus, rebuildIndex, reseedCorpus } from '../lib/api'
import { useStatusStore } from './status'
import type { LogEntry, RebuildResult, ReseedResult, SeedMode, SystemStatus } from '../types/admin'

/** 后台运维：系统状态轮询、索引重建、种子重导、内存日志。 */
export const useAdminOpsStore = defineStore('admin-ops', () => {
  const status = ref<SystemStatus | null>(null)
  const statusLoading = ref(false)
  /** status 拉取失败置真——仪表盘据此显示"后端不可达"而不是永远转骨架屏。 */
  const statusError = ref(false)
  const logs = ref<LogEntry[]>([])
  const logsLoading = ref(false)
  const logLevel = ref('Information')
  const logTake = ref(200)
  /** 重建/重导进行中（按钮 loading，防连点）。 */
  const busy = ref(false)

  async function refreshStatus(): Promise<void> {
    statusLoading.value = true
    try {
      status.value = await getSystemStatus()
      statusError.value = false
    } catch {
      statusError.value = true  // 保留旧数据，仅标记（10s 轮询不产生未处理拒绝）
    } finally {
      statusLoading.value = false
    }
  }

  async function rebuild(): Promise<RebuildResult> {
    busy.value = true
    try {
      const result = await rebuildIndex()
      await refreshStatus()
      return result
    } finally {
      busy.value = false
    }
  }

  async function reseed(mode: SeedMode): Promise<ReseedResult> {
    busy.value = true
    try {
      const result = await reseedCorpus(mode)
      // 语料变了 → 前台侧边栏块数同步
      await Promise.all([refreshStatus(), useStatusStore().refresh()])
      return result
    } finally {
      busy.value = false
    }
  }

  async function refreshLogs(): Promise<void> {
    logsLoading.value = true
    try {
      logs.value = await getLogs(logLevel.value, logTake.value)
    } catch {
      // 拉取失败保留旧数据（自动刷新轮询不产生未处理拒绝）
    } finally {
      logsLoading.value = false
    }
  }

  return {
    status, statusLoading, statusError, logs, logsLoading, logLevel, logTake, busy,
    refreshStatus, rebuild, reseed, refreshLogs,
  }
})
