<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSessionsStore } from '../stores/sessions'

const sessionsStore = useSessionsStore()
const route = useRoute()
const router = useRouter()

onMounted(sessionsStore.refresh)

const statusColor: Record<string, string> = {
  Completed: 'var(--ok)',
  Cancelled: '#d8a736',
  Failed: '#e56',
}
</script>

<template>
  <nav class="session-list">
    <p v-if="sessionsStore.sessions.length === 0" class="placeholder">还没有研究记录。</p>
    <button
      v-for="s in sessionsStore.sessions"
      :key="s.id"
      class="session-item"
      :class="{ current: String(s.id) === route.params.id }"
      :title="s.question"
      @click="router.push(`/sessions/${s.id}`)"
    >
      <span class="dot" :style="{ background: statusColor[s.status] ?? 'var(--text-dim)' }" />
      <span class="q">{{ s.question }}</span>
      <span class="time">{{ new Date(s.createdAt).toLocaleString() }}</span>
    </button>
  </nav>
</template>

<style scoped>
.session-list { flex: 1; overflow-y: auto; padding: 0 8px; }
.placeholder { color: var(--text-dim); font-size: 0.85rem; padding: 0 8px; }
.session-item {
  display: grid; grid-template-columns: 10px 1fr; grid-template-areas: 'dot q' 'dot time';
  column-gap: 8px; align-items: start; width: 100%; text-align: left;
  padding: 8px 10px; margin-bottom: 4px; border-radius: 8px; font-size: 0.85rem;
  background: transparent; border: 1px solid transparent; color: var(--text); cursor: pointer;
}
.session-item:hover { border-color: var(--accent); }
.session-item.current { background: var(--accent-soft); border-color: var(--accent); }
.dot { grid-area: dot; width: 8px; height: 8px; border-radius: 50%; margin-top: 5px; }
.q {
  grid-area: q; overflow: hidden; text-overflow: ellipsis;
  display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;
}
.time { grid-area: time; color: var(--text-dim); font-size: 0.74rem; }
</style>
