<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { useAdminOpsStore } from '../../stores/adminOps'

/** 系统状态仪表盘：10 秒轮询，离开页面停止。 */
const ops = useAdminOpsStore()

let timer: number | undefined
onMounted(() => {
  void ops.refreshStatus()
  timer = window.setInterval(() => { void ops.refreshStatus() }, 10_000)
})
onUnmounted(() => window.clearInterval(timer))

function formatUptime(totalSeconds: number): string {
  const s = Math.floor(totalSeconds)
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  if (h > 0) {
    return `${h} 小时 ${m} 分`
  }
  if (m > 0) {
    return `${m} 分 ${s % 60} 秒`
  }
  return `${s} 秒`
}
</script>

<template>
  <div class="dashboard">
    <div class="dash-head">
      <h2>系统状态总览</h2>
      <el-button :icon="Refresh" size="small" :loading="ops.statusLoading" @click="ops.refreshStatus">
        刷新
      </el-button>
    </div>

    <el-skeleton v-if="!ops.status && !ops.statusError" :rows="4" animated />
    <el-alert
      v-else-if="!ops.status && ops.statusError"
      type="error"
      :closable="false"
      title="无法获取系统状态：后端不可达？（本页 10 秒自动重试）"
    />
    <template v-else-if="ops.status">
      <div class="card-grid">
        <el-card shadow="never">
          <div class="stat-label">数据库</div>
          <el-tag :type="ops.status.dbConnected ? 'success' : 'danger'">
            {{ ops.status.dbConnected ? '已连接' : '连接失败' }}
          </el-tag>
        </el-card>
        <el-card shadow="never">
          <div class="stat-label">语料文档</div>
          <div class="stat-value">{{ ops.status.documentCount }}</div>
        </el-card>
        <el-card shadow="never">
          <div class="stat-label">会话记录</div>
          <div class="stat-value">{{ ops.status.sessionCount }}</div>
        </el-card>
        <el-card shadow="never">
          <div class="stat-label">索引块数</div>
          <div class="stat-value">{{ ops.status.corpusChunkCount }}</div>
        </el-card>
        <el-card shadow="never">
          <div class="stat-label">运行时长</div>
          <div class="stat-value">{{ formatUptime(ops.status.uptimeSeconds) }}</div>
        </el-card>
        <el-card shadow="never">
          <div class="stat-label">研究进行中</div>
          <el-tag :type="ops.status.isResearchRunning ? 'warning' : 'info'">
            {{ ops.status.isResearchRunning ? '是' : '否' }}
          </el-tag>
        </el-card>
      </div>

      <el-card shadow="never" class="detail-card">
        <template #header>详情</template>
        <p class="detail-line">
          <span class="stat-label">语料目录：</span>{{ ops.status.corpusDirectory ?? '未找到（种子重导不可用）' }}
        </p>
        <div class="detail-line">
          <span class="stat-label">已应用迁移：</span>
          <el-tag v-for="m in ops.status.appliedMigrations" :key="m" size="small" class="migration-tag">
            {{ m }}
          </el-tag>
          <el-text v-if="!ops.status.appliedMigrations.length" type="info">（无）</el-text>
        </div>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.dash-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
.dash-head h2 { margin: 0; font-size: 18px; }
.card-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 12px; }
.stat-label { color: var(--el-text-color-secondary); font-size: 13px; margin-bottom: 8px; }
.stat-value { font-size: 22px; font-weight: 600; }
.detail-card { margin-top: 16px; }
.detail-line { margin: 0 0 8px; }
.migration-tag { margin: 0 6px 6px 0; }
</style>
