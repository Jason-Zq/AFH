<script setup lang="ts">
import { onMounted } from 'vue'
import { useResearch } from '../lib/researchStore'

const { state, refreshSessions, loadSession } = useResearch()

onMounted(refreshSessions)

const statusLabel: Record<string, string> = {
  Completed: '✓',
  Cancelled: '⏹',
  Failed: '✗',
}
</script>

<template>
  <section class="history">
    <h2>历史会话</h2>
    <p v-if="state.sessions.length === 0" class="placeholder">还没有研究记录。</p>
    <ul>
      <li v-for="s in state.sessions" :key="s.id">
        <button class="history-item" :title="s.question" @click="loadSession(s.id)">
          <span class="history-status" :data-status="s.status">{{ statusLabel[s.status] ?? '?' }}</span>
          <span class="history-question">{{ s.question }}</span>
          <span class="history-time">{{ new Date(s.createdAt).toLocaleString() }}</span>
        </button>
      </li>
    </ul>
  </section>
</template>
