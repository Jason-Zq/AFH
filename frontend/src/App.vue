<script setup lang="ts">
import { onMounted, ref } from 'vue'
import AskBar from './components/AskBar.vue'
import TimelinePanel from './components/TimelinePanel.vue'
import ReportPanel from './components/ReportPanel.vue'
import HistoryPanel from './components/HistoryPanel.vue'
import { useResearch } from './lib/researchStore'
import { getStatus } from './lib/api'

const { state } = useResearch()
const corpusChunkCount = ref(0)

onMounted(async () => {
  try {
    corpusChunkCount.value = (await getStatus()).corpusChunkCount
  } catch {
    corpusChunkCount.value = 0
  }
})
</script>

<template>
  <div class="page">
    <header class="hero">
      <h1>多智能体研究助手</h1>
      <p class="subtitle">
        Microsoft Agent Framework Harness 演示 · Researcher → Analyst → Writer ⇄ Reviewer 图编排（修订最多 2 轮）·
        本地文档库（{{ corpusChunkCount }} 个检索块）+ 博查联网双通道
      </p>
    </header>

    <AskBar />

    <div v-if="state.error" class="error-banner">{{ state.error }}</div>

    <div class="panels">
      <HistoryPanel />
      <TimelinePanel />
      <ReportPanel />
    </div>
  </div>
</template>
