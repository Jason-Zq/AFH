<script setup lang="ts">
import { computed } from 'vue'
import { EXECUTOR_LABELS, STAGE_ORDER } from '../lib/executors'
import type { ExecutorBlock } from '../stores/research'

const props = defineProps<{ blocks: ExecutorBlock[] }>()

// 当前阶段 = 最后一个 block 在流水线中的位置；Finalize 出现即全部完成
const activeIndex = computed(() => {
  const last = props.blocks[props.blocks.length - 1]
  if (!last) return -1
  if (last.name === 'Finalize') return STAGE_ORDER.length
  const idx = STAGE_ORDER.indexOf(last.name as (typeof STAGE_ORDER)[number])
  return idx
})

function stepStatus(index: number): 'wait' | 'process' | 'success' {
  if (index < activeIndex.value) return 'success'
  if (index === activeIndex.value) return 'process'
  return 'wait'
}

// 修订轮次 = 同名 block 出现次数（审校打回回环的可视化）
function rounds(name: string): number {
  return props.blocks.filter(b => b.name === name).length
}
</script>

<template>
  <el-steps class="stage-stepper" :active="activeIndex" align-center>
    <el-step
      v-for="(stage, i) in STAGE_ORDER"
      :key="stage"
      :status="stepStatus(i)"
    >
      <template #title>
        <span class="stage-title">
          {{ EXECUTOR_LABELS[stage] }}
          <el-badge v-if="rounds(stage) > 1" :value="`第${rounds(stage)}轮`" type="warning" class="round-badge" />
        </span>
      </template>
      <template #description>{{ stage }}</template>
    </el-step>
  </el-steps>
</template>

<style scoped>
.stage-stepper { padding: 4px 0 14px; }
.round-badge { margin-left: 6px; }
</style>
