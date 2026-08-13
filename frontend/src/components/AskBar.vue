<script setup lang="ts">
import { useResearch } from '../lib/researchStore'

const { state, start, cancel } = useResearch()

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !state.running) void start()
}
</script>

<template>
  <div class="ask-bar">
    <input
      v-model="state.question"
      placeholder="例如：Agent Harness 和 Agent Framework 有什么区别？"
      :disabled="state.running"
      @keydown="onKeydown"
    />
    <button class="primary" :disabled="state.running || !state.question.trim()" @click="start">
      {{ state.running ? '研究进行中…' : '开始研究' }}
    </button>
    <button v-if="state.running" class="ghost" @click="cancel">取消</button>
  </div>

  <div class="switches">
    <span class="switches-label">Harness 特性开关（作用于 Researcher）：</span>
    <label><input type="checkbox" v-model="state.switches.todoList" :disabled="state.running" /> 待办列表</label>
    <label><input type="checkbox" v-model="state.switches.planExecuteMode" :disabled="state.running" /> plan/execute 模式</label>
    <label><input type="checkbox" v-model="state.switches.fileMemory" :disabled="state.running" /> 文件记忆</label>
    <label><input type="checkbox" v-model="state.switches.skillDiscovery" :disabled="state.running" /> 技能发现</label>
  </div>
</template>
