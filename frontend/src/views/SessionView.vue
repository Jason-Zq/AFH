<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getSession } from '../lib/api'
import ReportView from '../components/ReportView.vue'
import type { SessionDetail } from '../types/events'

const route = useRoute()
const detail = ref<SessionDetail | null>(null)
const loadError = ref<string | null>(null)

const statusType: Record<string, 'success' | 'warning' | 'danger'> = {
  Completed: 'success',
  Cancelled: 'warning',
  Failed: 'danger',
}
const statusText: Record<string, string> = {
  Completed: '已完成',
  Cancelled: '已取消',
  Failed: '失败',
}

const switchLabels: Record<string, string> = {
  todoList: '待办列表',
  planExecuteMode: 'plan/execute',
  fileMemory: '文件记忆',
  skillDiscovery: '技能发现',
}

function parseSwitches(json: string): string[] {
  try {
    return Object.entries(JSON.parse(json) as Record<string, boolean>)
      .filter(([, v]) => v)
      .map(([k]) => switchLabels[k] ?? k)
  } catch {
    return []
  }
}

watch(() => route.params.id, async id => {
  detail.value = null
  loadError.value = null
  try {
    detail.value = await getSession(Number(id))
  } catch (ex) {
    loadError.value = `加载历史会话失败：${ex instanceof Error ? ex.message : String(ex)}`
  }
}, { immediate: true })
</script>

<template>
  <div class="session-view">
    <el-alert v-if="loadError" :title="loadError" type="error" :closable="false" />
    <template v-else-if="detail">
      <header class="session-meta">
        <h2 class="session-question">{{ detail.question }}</h2>
        <div class="meta-line">
          <el-tag :type="statusType[detail.status] ?? 'info'">{{ statusText[detail.status] ?? detail.status }}</el-tag>
          <span class="time">{{ new Date(detail.createdAt).toLocaleString() }}</span>
          <el-tag v-for="sw in parseSwitches(detail.switches)" :key="sw" size="small" effect="plain">{{ sw }}</el-tag>
        </div>
      </header>
      <ReportView :markdown="detail.reportMarkdown" />
    </template>
  </div>
</template>

<style scoped>
.session-view { height: 100%; display: flex; flex-direction: column; padding: 20px 24px; min-height: 0; }
.session-meta { margin-bottom: 14px; }
.session-question { margin: 0 0 8px; font-size: 1.2rem; }
.meta-line { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.time { color: var(--text-dim); font-size: 0.85rem; }
</style>
