import { onMounted, onUnmounted, ref } from 'vue'

/** 响应式断点侦听（App 壳与工作区共用）。 */
export function useMediaQuery(query: string) {
  const mql = window.matchMedia(query)
  const matches = ref(mql.matches)
  const update = (e: MediaQueryListEvent) => { matches.value = e.matches }
  onMounted(() => mql.addEventListener('change', update))
  onUnmounted(() => mql.removeEventListener('change', update))
  return matches
}
