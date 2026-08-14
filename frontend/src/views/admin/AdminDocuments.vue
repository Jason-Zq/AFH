<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import DocumentEditDialog from '../../components/DocumentEditDialog.vue'
import { useAdminDocumentsStore } from '../../stores/adminDocuments'

const store = useAdminDocumentsStore()
const dialog = ref<InstanceType<typeof DocumentEditDialog>>()

onMounted(store.refresh)

function formatTime(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN', { hour12: false })
}

function doSearch(): void {
  store.page = 1
  void store.refresh()
}

async function openDialog(id: number | null): Promise<void> {
  try {
    await dialog.value?.open(id)
  } catch {
    ElMessage.error('加载文档详情失败')
  }
}

function onSaved(chunkCount: number | null): void {
  if (chunkCount === null) {
    ElMessage.warning('已保存，但索引自动重建失败——请到「运维操作」页手动重建')
  } else {
    ElMessage.success(`已保存，索引已自动重建：${chunkCount} 块`)
  }
}

async function remove(id: number, name: string): Promise<void> {
  try {
    await ElMessageBox.confirm(
      `确定删除「${name}」？删除后立即从 BM25 索引中消失，不可恢复。`,
      '删除语料文档',
      { type: 'warning', confirmButtonText: '删除', confirmButtonClass: 'el-button--danger' },
    )
  } catch {
    return // 用户取消
  }
  try {
    await store.remove(id)
    ElMessage.success('已删除')
  } catch (error) {
    ElMessage.error(error instanceof Error ? error.message : '删除失败')
  }
}
</script>

<template>
  <div class="admin-documents">
    <div class="toolbar">
      <el-input
        v-model="store.search"
        placeholder="搜索文档名或内容…"
        :prefix-icon="Search"
        clearable
        class="search-input"
        @keyup.enter="doSearch"
        @clear="doSearch"
      />
      <el-button :icon="Refresh" :loading="store.loading" @click="store.refresh">刷新</el-button>
      <el-button type="primary" :icon="Plus" @click="openDialog(null)">新增文档</el-button>
    </div>

    <el-table v-loading="store.loading" :data="store.items" stripe>
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="name" label="文档名" min-width="220" show-overflow-tooltip />
      <el-table-column prop="contentPreview" label="内容预览" min-width="320" show-overflow-tooltip />
      <el-table-column prop="contentLength" label="长度" width="90">
        <template #default="{ row }">{{ row.contentLength }} 字</template>
      </el-table-column>
      <el-table-column label="更新时间" width="170">
        <template #default="{ row }">{{ formatTime(row.updatedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button size="small" text type="primary" @click="openDialog(row.id)">编辑</el-button>
          <el-button size="small" text type="danger" @click="remove(row.id, row.name)">删除</el-button>
        </template>
      </el-table-column>
      <template #empty><el-empty description="暂无语料文档" /></template>
    </el-table>

    <el-pagination
      v-model:current-page="store.page"
      v-model:page-size="store.pageSize"
      :total="store.total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next"
      class="pager"
      @current-change="store.refresh"
      @size-change="doSearch"
    />

    <DocumentEditDialog ref="dialog" @saved="onSaved" />
  </div>
</template>

<style scoped>
.toolbar { display: flex; gap: 8px; margin-bottom: 12px; }
.search-input { max-width: 320px; }
.pager { margin-top: 12px; justify-content: flex-end; }
</style>
