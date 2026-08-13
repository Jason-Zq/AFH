<script setup lang="ts">
import { useResearch } from '../lib/researchStore'

const { state } = useResearch()
</script>

<template>
  <section class="timeline">
    <h2>协作过程</h2>
    <p v-if="state.blocks.length === 0" class="placeholder">
      输入研究问题后，这里会实时展示各 Agent 的输出与工具调用。
    </p>
    <div
      v-for="block in state.blocks"
      :key="block.name + block.items.length"
      class="executor"
      :class="block.active ? 'active' : 'done'"
    >
      <div class="executor-head">
        <span class="executor-name">{{ block.name }}</span>
        <span class="executor-status">{{ block.active ? '● 工作中' : '✓ 完成' }}</span>
      </div>
      <template v-for="(item, i) in block.items" :key="i">
        <div v-if="item.kind === 'text'" class="agent-text">{{ item.content }}</div>
        <div v-else class="tool-call">
          <div class="tool-head">🔧 {{ item.name }} <span class="tool-args">{{ item.args }}</span></div>
          <div v-if="item.result !== null" class="tool-result">{{ item.result }}</div>
        </div>
      </template>
    </div>
  </section>
</template>
