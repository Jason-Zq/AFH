<script setup lang="ts">
import { Setting } from '@element-plus/icons-vue'
import { useResearchStore } from '../stores/research'

const research = useResearchStore()
</script>

<template>
  <el-popover placement="bottom-end" :width="260" trigger="click">
    <template #reference>
      <el-button size="large" :icon="Setting" title="高级选项" />
    </template>
    <p class="switch-hint">Harness 特性开关（作用于 Researcher）</p>
    <div class="switch-list">
      <el-checkbox v-model="research.switches.todoList" :disabled="research.running">待办列表</el-checkbox>
      <el-checkbox v-model="research.switches.planExecuteMode" :disabled="research.running">plan/execute 模式</el-checkbox>
      <el-checkbox v-model="research.switches.fileMemory" :disabled="research.running">文件记忆</el-checkbox>
      <el-checkbox v-model="research.switches.skillDiscovery" :disabled="research.running">技能发现</el-checkbox>
    </div>
  </el-popover>
</template>

<style scoped>
.switch-hint { margin: 0 0 8px; font-size: 0.82rem; color: var(--text-dim); }
.switch-list { display: flex; flex-direction: column; align-items: flex-start; }
</style>
