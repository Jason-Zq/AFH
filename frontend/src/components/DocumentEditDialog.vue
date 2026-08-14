<script setup lang="ts">
import { reactive, ref } from 'vue'
import { getAdminDocument } from '../lib/api'
import { useAdminDocumentsStore } from '../stores/adminDocuments'
import type { DocumentInput } from '../types/admin'

/** 语料新增/编辑对话框。编辑模式打开时才拉全文（列表只有 200 字预览）。 */
const emit = defineEmits<{ saved: [chunkCount: number | null] }>()

const store = useAdminDocumentsStore()

const visible = ref(false)
const saving = ref(false)
const editingId = ref<number | null>(null)
const form = reactive<DocumentInput>({ name: '', content: '' })
/** 409 重名等后端错误直接挂在表单顶部（比全局 ElMessage 更贴上下文）。 */
const formError = ref('')

async function open(id: number | null): Promise<void> {
  formError.value = ''
  editingId.value = id
  if (id === null) {
    form.name = ''
    form.content = ''
  } else {
    const doc = await getAdminDocument(id)
    form.name = doc.name
    form.content = doc.content
  }
  visible.value = true
}

async function save(): Promise<void> {
  if (!form.name.trim() || !form.content.trim()) {
    formError.value = '文档名和内容都不能为空。'
    return
  }
  saving.value = true
  formError.value = ''
  try {
    const chunkCount = editingId.value === null
      ? await store.create({ name: form.name.trim(), content: form.content })
      : await store.update(editingId.value, { name: form.name.trim(), content: form.content })
    visible.value = false
    // 成功提示由父组件统一发（需要区分 chunkCount 为 null 的降级场景）
    emit('saved', chunkCount)
  } catch (error) {
    formError.value = error instanceof Error ? error.message : '保存失败'
  } finally {
    saving.value = false
  }
}

defineExpose({ open })
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="editingId === null ? '新增语料文档' : '编辑语料文档'"
    width="640px"
    :close-on-click-modal="false"
  >
    <el-alert v-if="formError" :title="formError" type="error" :closable="false" class="form-error" />
    <el-form label-position="top">
      <el-form-item label="文档名（唯一，按名称排序建索引）" required>
        <el-input v-model="form.name" maxlength="200" show-word-limit placeholder="如：agentic-rag.md" />
      </el-form-item>
      <el-form-item label="内容（Markdown 全文）" required>
        <el-input v-model="form.content" type="textarea" :rows="14" placeholder="粘贴 Markdown 内容…" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="saving" @click="save">保存（自动重建索引）</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.form-error { margin-bottom: 12px; }
</style>
