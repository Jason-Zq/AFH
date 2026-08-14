<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { useAdminOpsStore } from '../../stores/adminOps'

/** 内存环形日志：最新在前；自动刷新 5s 一档，离开页面即停。 */
const ops = useAdminOpsStore()
const autoRefresh = ref(false)

let timer: number | undefined
watch(autoRefresh, on => {
  if (on) {
    timer = window.setInterval(() => { void ops.refreshLogs() }, 5_000)
  } else {
    window.clearInterval(timer)
  }
})

onMounted(() => { void ops.refreshLogs() })
onUnmounted(() => window.clearInterval(timer))

const LEVEL_TYPE: Record<string, 'info' | 'warning' | 'danger' | 'primary'> = {
  Trace: 'info',
  Debug: 'info',
  Information: 'primary',
  Warning: 'warning',
  Error: 'danger',
  Critical: 'danger',
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('zh-CN', { hour12: false })
}

/** 分类名只留最后一段（ResearchAssistant.Web.Services.ResearchRunner → ResearchRunner）。 */
function shortCategory(category: string): string {
  return category.split('.').pop() ?? category
}
</script>

<template>
  <div class="admin-logs">
    <div class="toolbar">
      <el-select v-model="ops.logLevel" class="level-filter" @change="ops.refreshLogs">
        <!-- 后端过滤下限是 Information（Program.cs AddFilter），更低的级别永远不会入库，不列出 -->
        <el-option label="Information 及以上" value="Information" />
        <el-option label="Warning 及以上" value="Warning" />
        <el-option label="Error 及以上" value="Error" />
      </el-select>
      <el-select v-model="ops.logTake" class="take-filter" @change="ops.refreshLogs">
        <el-option label="最近 100 条" :value="100" />
        <el-option label="最近 200 条" :value="200" />
        <el-option label="最近 500 条" :value="500" />
        <el-option label="最近 1000 条" :value="1000" />
      </el-select>
      <el-button :icon="Refresh" :loading="ops.logsLoading" @click="ops.refreshLogs">刷新</el-button>
      <el-switch v-model="autoRefresh" active-text="自动刷新（5s）" />
      <el-text type="info" size="small">内存环形缓冲（1000 条），重启即清空</el-text>
    </div>

    <el-table v-loading="ops.logsLoading" :data="ops.logs" stripe size="small">
      <el-table-column type="expand">
        <template #default="{ row }">
          <pre v-if="row.exception" class="exception">{{ row.exception }}</pre>
          <el-text v-else type="info">无异常堆栈</el-text>
        </template>
      </el-table-column>
      <el-table-column label="时间" width="100">
        <template #default="{ row }">{{ formatTime(row.timestamp) }}</template>
      </el-table-column>
      <el-table-column label="级别" width="110">
        <template #default="{ row }">
          <el-tag :type="LEVEL_TYPE[row.level] ?? 'info'" size="small">{{ row.level }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="来源" width="200" show-overflow-tooltip>
        <template #default="{ row }">
          <span :title="row.category">{{ shortCategory(row.category) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="message" label="消息" min-width="420" show-overflow-tooltip />
      <template #empty><el-empty description="暂无日志（重启后缓冲区为空）" /></template>
    </el-table>
  </div>
</template>

<style scoped>
.toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 12px; }
.level-filter { width: 170px; }
.take-filter { width: 140px; }
.exception {
  margin: 0; padding: 8px 12px; white-space: pre-wrap; word-break: break-all;
  font-size: 12px; color: var(--el-color-danger);
}
</style>
