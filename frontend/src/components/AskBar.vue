<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Promotion } from '@element-plus/icons-vue'
import { useResearchStore } from '../stores/research'
import SwitchPopover from './SwitchPopover.vue'

const research = useResearchStore()
const router = useRouter()

function submit(): void {
  if (research.running || !research.question.trim()) return
  void research.start() // 不 await：立即跳转工作区，边看边跑
  router.push('/run')
}
</script>

<template>
  <div class="ask-bar">
    <el-input
      v-model="research.question"
      size="large"
      placeholder="例如：Agent Harness 和 Agent Framework 有什么区别？"
      :disabled="research.running"
      clearable
      @keydown.enter="submit"
    />
    <el-button
      type="primary"
      size="large"
      :icon="Promotion"
      :disabled="research.running || !research.question.trim()"
      @click="submit"
    >
      开始研究
    </el-button>
    <SwitchPopover />
  </div>
</template>

<style scoped>
.ask-bar { display: flex; gap: 10px; width: 100%; }
.ask-bar :deep(.el-input__wrapper) { padding: 6px 16px; }
</style>
