import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getStatus } from '../lib/api'

/** 后端/语料状态（侧栏与首页共用一份，避免重复请求）。 */
export const useStatusStore = defineStore('status', () => {
  const corpusChunkCount = ref(0)

  async function refresh(): Promise<void> {
    try {
      corpusChunkCount.value = (await getStatus()).corpusChunkCount
    } catch {
      corpusChunkCount.value = 0
    }
  }

  return { corpusChunkCount, refresh }
})
