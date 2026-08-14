<script setup lang="ts">
import { computed } from 'vue'
import { CircleCheck, Loading } from '@element-plus/icons-vue'
import { EXECUTOR_LABELS } from '../lib/executors'
import type { ExecutorBlock as Block } from '../stores/research'
import ToolCallItem from './ToolCallItem.vue'

const props = defineProps<{ block: Block }>()

const label = computed(() => EXECUTOR_LABELS[props.block.name] ?? props.block.name)
const elapsed = computed(() => {
  if (props.block.endedAt === null) return null
  return `${Math.max(1, Math.round((props.block.endedAt - props.block.startedAt) / 1000))}s`
})
</script>

<template>
  <el-card class="executor-block" :class="{ active: block.active }" shadow="never">
    <template #header>
      <div class="head">
        <span class="name">
          {{ block.name }}
          <el-tag size="small" effect="plain">{{ label }}</el-tag>
        </span>
        <span class="status">
          <template v-if="block.active">
            <el-icon class="is-loading"><Loading /></el-icon> 工作中
          </template>
          <template v-else>
            <el-icon color="var(--ok)"><CircleCheck /></el-icon> {{ elapsed }}
          </template>
        </span>
      </div>
    </template>
    <template v-for="(item, i) in block.items" :key="i">
      <div v-if="item.kind === 'text'" class="agent-text">{{ item.content }}</div>
      <ToolCallItem v-else :item="item" />
    </template>
  </el-card>
</template>

<style scoped>
.executor-block { margin-bottom: 12px; background: var(--panel); }
.executor-block.active { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-soft); }
.head { display: flex; justify-content: space-between; align-items: center; }
.name { font-weight: 700; color: var(--accent); display: inline-flex; align-items: center; gap: 8px; }
.status { color: var(--text-dim); font-size: 0.85rem; display: inline-flex; align-items: center; gap: 4px; }
.agent-text { white-space: pre-wrap; line-height: 1.65; font-size: 0.95rem; margin: 6px 0; }
</style>
