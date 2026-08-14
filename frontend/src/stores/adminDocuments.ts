import { defineStore } from 'pinia'
import { ref } from 'vue'
import { createDocument, deleteDocument, listAdminDocuments, updateDocument } from '../lib/api'
import { useStatusStore } from './status'
import type { DocumentInput, DocumentSummary } from '../types/admin'

/** 后台语料管理：分页列表 + 增删改。变更后联动刷新前台语料块数。 */
export const useAdminDocumentsStore = defineStore('admin-documents', () => {
  const items = ref<DocumentSummary[]>([])
  const total = ref(0)
  const page = ref(1)
  const pageSize = ref(20)
  const search = ref('')
  const loading = ref(false)

  async function refresh(): Promise<void> {
    loading.value = true
    try {
      const data = await listAdminDocuments(page.value, pageSize.value, search.value)
      items.value = data.items
      total.value = data.total
    } catch {
      // 拉取失败保留旧数据（fire-and-forget 调用不产生未处理拒绝）
    } finally {
      loading.value = false
    }
  }

  // 写操作的错误原样抛出，由组件用 ElMessage 反馈（store 不碰 UI）；返回重建后的块数（null = 重建失败）供提示
  async function create(input: DocumentInput): Promise<number | null> {
    const { corpusChunkCount } = await createDocument(input)
    await afterMutation()
    return corpusChunkCount
  }

  async function update(id: number, input: DocumentInput): Promise<number | null> {
    const { corpusChunkCount } = await updateDocument(id, input)
    await afterMutation()
    return corpusChunkCount
  }

  async function remove(id: number): Promise<void> {
    await deleteDocument(id)
    await afterMutation()
  }

  /** 后端写操作会自动重建索引，这里同步前台侧边栏的语料块数。 */
  async function afterMutation(): Promise<void> {
    await Promise.all([refresh(), useStatusStore().refresh()])
  }

  return { items, total, page, pageSize, search, loading, refresh, create, update, remove }
})
