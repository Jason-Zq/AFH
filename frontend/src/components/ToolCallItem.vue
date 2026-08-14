<script setup lang="ts">
import { computed, ref } from 'vue'
import { ArrowDown, ArrowRight, Document, Link, Tools } from '@element-plus/icons-vue'
import type { ToolItem } from '../stores/research'

const props = defineProps<{ item: ToolItem }>()

const collapsed = ref(true)
const toolIcon = computed(() => {
  if (props.item.name.includes('local')) return Document
  if (props.item.name.includes('web')) return Link
  return Tools
})
</script>

<template>
  <div class="tool-call">
    <button class="tool-head" @click="collapsed = !collapsed">
      <el-icon class="chevron">
        <ArrowRight v-if="collapsed" />
        <ArrowDown v-else />
      </el-icon>
      <el-icon><component :is="toolIcon" /></el-icon>
      <span class="tool-name">{{ item.name }}</span>
      <span class="tool-args">{{ item.args }}</span>
    </button>
    <div v-show="!collapsed" class="tool-result">{{ item.result ?? '等待返回…' }}</div>
  </div>
</template>

<style scoped>
.tool-call {
  background: var(--tool-bg); border-left: 3px solid var(--accent);
  border-radius: 6px; margin: 8px 0; font-size: 0.88rem;
}
.tool-head {
  display: flex; align-items: center; gap: 6px; width: 100%;
  padding: 8px 12px; background: transparent; border: none;
  color: var(--text); font-size: 0.88rem; cursor: pointer; text-align: left;
}
.tool-name { font-weight: 600; flex-shrink: 0; }
.tool-args {
  color: var(--text-dim); overflow: hidden;
  text-overflow: ellipsis; white-space: nowrap;
}
.tool-result {
  color: var(--text-dim); padding: 0 12px 10px 34px; white-space: pre-wrap;
  max-height: 220px; overflow-y: auto; font-size: 0.84rem;
}
</style>
