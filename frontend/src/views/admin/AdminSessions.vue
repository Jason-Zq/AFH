<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Refresh } from '@element-plus/icons-vue'
import ReportView from '../../components/ReportView.vue'
import { getSession } from '../../lib/api'
import { useAdminSessionsStore } from '../../stores/adminSessions'
import type { AdminSessionSummary } from '../../types/admin'
import type { SessionDetail } from '../../types/events'

const store = useAdminSessionsStore()

onMounted(store.refresh)

const STATUS_META: Record<string, { label: string; type: 'success' | 'info' | 'danger' }> = {
  Completed: { label: '已完成', type: 'success' },
  Cancelled: { label: '已取消', type: 'info' },
  Failed: { label: '失败', type: 'danger' },
}
const statusMeta = (s: string) => STATUS_META[s] ?? { label: s, type: 'info' as const }

function formatTime(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN', { hour12: false })
}

// ===== 查看报告（复用前台 ReportView，数据走已有的 /api/sessions/{id}） =====
const drawerOpen = ref(false)
const detail = ref<SessionDetail | null>(null)
const detailLoading = ref(false)

async function view(id: number): Promise<void> {
  drawerOpen.value = true
  detailLoading.value = true
  detail.value = null
  try {
    detail.value = await getSession(id)
  } catch {
    ElMessage.error('加载会话详情失败')
    drawerOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

// ===== 删除 =====
const selection = ref<number[]>([])
const hasSelection = computed(() => selection.value.length > 0)

function onSelectionChange(rows: AdminSessionSummary[]): void {
  selection.value = rows.map(r => r.id)
}

async function removeOne(id: number): Promise<void> {
  try {
    await store.removeOne(id)
    ElMessage.success('已删除')
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '删除失败')
  }
}

async function removeSelected(): Promise<void> {
  const count = selection.value.length
  try {
    await ElMessageBox.confirm(
      `确定删除选中的 ${count} 条会话记录？报告全文一并删除，不可恢复。`,
      '批量删除会话',
      { type: 'warning', confirmButtonText: `删除 ${count} 条`, confirmButtonClass: 'el-button--danger' },
    )
  } catch {
    return // 用户取消
  }
  try {
    const deleted = await store.removeBatch(selection.value)
    ElMessage.success(`已删除 ${deleted} 条`)
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '批量删除失败')
  }
}
</script>

<template>
  <div class="admin-sessions">
    <div class="toolbar">
      <el-select v-model="store.status" placeholder="全部状态" clearable class="status-filter" @change="store.page = 1; store.refresh()">
        <el-option label="已完成" value="Completed" />
        <el-option label="已取消" value="Cancelled" />
        <el-option label="失败" value="Failed" />
      </el-select>
      <el-button :icon="Refresh" :loading="store.loading" @click="store.refresh">刷新</el-button>
      <el-button
        type="danger"
        :icon="Delete"
        :disabled="!hasSelection"
        @click="removeSelected"
      >
        批量删除{{ hasSelection ? `（${selection.length}）` : '' }}
      </el-button>
    </div>

    <el-table
      v-loading="store.loading"
      :data="store.items"
      stripe
      @selection-change="onSelectionChange"
    >
      <el-table-column type="selection" width="45" />
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="question" label="问题" min-width="320" show-overflow-tooltip />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="statusMeta(row.status).type" size="small">{{ statusMeta(row.status).label }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="报告" width="100">
        <template #default="{ row }">
          {{ row.reportLength > 0 ? `${row.reportLength} 字` : '—' }}
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button size="small" text type="primary" @click="view(row.id)">查看</el-button>
          <el-popconfirm title="删除该会话及其报告？" confirm-button-text="删除" cancel-button-text="取消" @confirm="removeOne(row.id)">
            <template #reference>
              <el-button size="small" text type="danger">删除</el-button>
            </template>
          </el-popconfirm>
        </template>
      </el-table-column>
      <template #empty><el-empty description="暂无会话记录" /></template>
    </el-table>

    <el-pagination
      v-model:current-page="store.page"
      v-model:page-size="store.pageSize"
      :total="store.total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next"
      class="pager"
      @current-change="store.refresh"
      @size-change="store.page = 1; store.refresh()"
    />

    <el-drawer v-model="drawerOpen" size="60%" :title="detail ? detail.question : '会话报告'">
      <ReportView :markdown="detail?.reportMarkdown ?? null" :pending="detailLoading" />
    </el-drawer>
  </div>
</template>

<style scoped>
.toolbar { display: flex; gap: 8px; margin-bottom: 12px; }
.status-filter { width: 140px; }
.pager { margin-top: 12px; justify-content: flex-end; }
</style>
