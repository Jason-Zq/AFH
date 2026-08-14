<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { useResearchStore } from '../stores/research'
import ExecutorBlock from './ExecutorBlock.vue'

const research = useResearchStore()

const container = ref<HTMLElement>()
const stick = ref(true) // 贴底时才自动跟随滚动；用户上翻则暂停

function onScroll(): void {
  const el = container.value
  if (!el) return
  stick.value = el.scrollHeight - el.scrollTop - el.clientHeight < 60
}

function toBottom(): void {
  container.value?.scrollTo({ top: container.value.scrollHeight, behavior: 'smooth' })
  stick.value = true
}

watch(() => research.blocks, async () => {
  await nextTick()
  if (stick.value) container.value?.scrollTo({ top: container.value.scrollHeight })
}, { deep: true })
</script>

<template>
  <div ref="container" class="activity-stream" @scroll="onScroll">
    <el-empty
      v-if="research.blocks.length === 0"
      description="研究开始后，这里实时展示各 Agent 的输出与工具调用。"
    />
    <ExecutorBlock v-for="(block, i) in research.blocks" :key="block.name + i" :block="block" />
    <el-button v-show="!stick" class="to-bottom" size="small" @click="toBottom">↓ 回到底部</el-button>
  </div>
</template>

<style scoped>
.activity-stream { position: relative; overflow-y: auto; min-height: 0; padding-right: 4px; }
.to-bottom { position: sticky; bottom: 8px; left: 50%; transform: translateX(-50%); display: flex; margin: 0 auto; }
</style>
