<script setup lang="ts">
import { useRouter } from 'vue-router'
import AskBar from '../components/AskBar.vue'
import { useResearchStore } from '../stores/research'
import { useStatusStore } from '../stores/status'

const research = useResearchStore()
const status = useStatusStore()
const router = useRouter()

const examples = [
  'Agent 工作流的检查点机制有什么用？和普通无状态请求有什么区别？',
  'BM25 和向量检索怎么融合？',
  'MCP 协议解决什么问题？',
]

function ask(q: string): void {
  if (research.running) return
  research.question = q
  void research.start()
  router.push('/run')
}
</script>

<template>
  <div class="home">
    <div class="hero">
      <h1>多智能体研究助手</h1>
      <p class="subtitle">
        Researcher → Analyst → Writer ⇄ Reviewer 图编排（修订最多 2 轮）·
        本地文档库（{{ status.corpusChunkCount }} 个检索块）+ 博查联网双通道
      </p>
      <AskBar />
      <div class="examples">
        <el-tag
          v-for="q in examples"
          :key="q"
          class="example"
          size="large"
          effect="plain"
          @click="ask(q)"
        >{{ q }}</el-tag>
      </div>
    </div>
  </div>
</template>

<style scoped>
.home { height: 100%; display: flex; align-items: center; justify-content: center; overflow-y: auto; }
.hero { width: min(720px, 90%); text-align: center; padding-bottom: 8vh; }
.hero h1 { margin: 0 0 10px; font-size: 1.7rem; }
.subtitle { color: var(--text-dim); margin: 0 0 28px; font-size: 0.92rem; }
.examples { display: flex; flex-direction: column; align-items: center; gap: 8px; margin-top: 20px; }
.example { cursor: pointer; max-width: 100%; height: auto; padding: 8px 14px; white-space: normal; }
.example:hover { color: var(--accent); border-color: var(--accent); }
</style>
