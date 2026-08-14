<script setup lang="ts">
import { onMounted } from 'vue'
import { Moon, Plus, Setting, Sunny } from '@element-plus/icons-vue'
import SessionList from './SessionList.vue'
import { useStatusStore } from '../stores/status'
import { useTheme } from '../composables/useTheme'

const status = useStatusStore()
const { isDark, toggle } = useTheme()

onMounted(status.refresh)
</script>

<template>
  <aside class="app-sidebar">
    <div class="brand">🔬 多智能体研究助手</div>
    <el-button type="primary" :icon="Plus" class="new-btn" @click="$router.push('/')">新研究</el-button>
    <SessionList />
    <div class="sidebar-footer">
      <span class="corpus">语料 {{ status.corpusChunkCount }} 块</span>
      <el-button :icon="Setting" circle title="后台管理" @click="$router.push('/admin')" />
      <el-button :icon="isDark ? Sunny : Moon" circle title="切换明暗主题" @click="toggle" />
    </div>
  </aside>
</template>

<style scoped>
.app-sidebar {
  width: 260px; flex-shrink: 0; height: 100vh; position: sticky; top: 0;
  display: flex; flex-direction: column;
  background: var(--panel); border-right: 1px solid var(--panel-border);
}
.brand { padding: 16px 16px 12px; font-weight: 700; font-size: 0.95rem; }
.new-btn { margin: 0 12px 12px; }
.sidebar-footer {
  margin-top: auto; padding: 10px 16px; display: flex;
  align-items: center; gap: 8px;
  border-top: 1px solid var(--panel-border);
}
.corpus { color: var(--text-dim); font-size: 0.8rem; margin-right: auto; }
</style>
