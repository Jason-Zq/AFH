<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Download, Refresh } from '@element-plus/icons-vue'
import { useAdminOpsStore } from '../../stores/adminOps'
import type { SeedMode } from '../../types/admin'

const ops = useAdminOpsStore()

// 研究进行中的闸门依赖 status，进页面先拉一次
onMounted(() => { void ops.refreshStatus() })

const rebuildResult = ref('')
const reseedMode = ref<SeedMode>('append')
const reseedResult = ref('')

async function rebuild(): Promise<void> {
  try {
    const r = await ops.rebuild()
    rebuildResult.value = `完成：${r.chunkCount} 块，耗时 ${r.elapsedMs}ms`
    ElMessage.success('索引重建完成')
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '重建失败')
  }
}

async function reseed(): Promise<void> {
  if (reseedMode.value === 'replace') {
    try {
      await ElMessageBox.confirm(
        'Replace 模式会清空 documents 表再导入种子文件，手工新增的语料将全部丢失。确定继续？',
        '危险操作确认',
        { type: 'error', confirmButtonText: '清空并重导', confirmButtonClass: 'el-button--danger' },
      )
    } catch {
      return // 用户取消
    }
  }
  try {
    const r = await ops.reseed(reseedMode.value)
    reseedResult.value = `导入 ${r.imported}、跳过重名 ${r.skipped}、删除 ${r.deleted}，索引重建为 ${r.chunkCount} 块`
    ElMessage.success('种子语料重导完成')
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '重导失败')
  }
}
</script>

<template>
  <div class="admin-ops">
    <el-alert
      v-if="ops.status?.isResearchRunning"
      type="warning"
      :closable="false"
      title="有研究正在进行：Replace 重导已被禁用（Rebuild 安全，进行中研究持旧索引）。"
      class="running-alert"
    />

    <el-card shadow="never" class="ops-card">
      <template #header>
        <div class="card-head">
          <span>重建 BM25 索引</span>
          <el-button type="primary" :icon="Refresh" :loading="ops.busy" @click="rebuild">立即重建</el-button>
        </div>
      </template>
      <p class="ops-desc">
        从 documents 表重新分块并构建索引，原子替换旧索引——进行中的研究继续使用旧索引，不受影响。
        语料写操作（增删改、种子重导）已自动触发重建，此按钮用于兜底（如手工改过数据库）。
      </p>
      <el-text v-if="rebuildResult" type="success">{{ rebuildResult }}</el-text>
    </el-card>

    <el-card shadow="never" class="ops-card">
      <template #header>
        <div class="card-head">
          <span>重新导入种子语料</span>
          <el-button
            type="primary"
            :icon="Download"
            :loading="ops.busy"
            :disabled="reseedMode === 'replace' && ops.status?.isResearchRunning"
            @click="reseed"
          >
            执行导入
          </el-button>
        </div>
      </template>
      <p class="ops-desc">从 data/corpus 目录的 *.md 种子文件导入语料，完成后自动重建索引。</p>
      <el-radio-group v-model="reseedMode">
        <el-radio value="append">Append：跳过重名，幂等追加</el-radio>
        <el-radio value="replace">Replace：清空重灌（事务包裹，危险）</el-radio>
      </el-radio-group>
      <div v-if="reseedResult" class="reseed-result">
        <el-text type="success">{{ reseedResult }}</el-text>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.running-alert { margin-bottom: 12px; }
.ops-card { margin-bottom: 16px; }
.card-head { display: flex; align-items: center; justify-content: space-between; }
.ops-desc { margin: 0 0 12px; color: var(--el-text-color-secondary); font-size: 13px; line-height: 1.6; }
.reseed-result { margin-top: 12px; }
</style>
