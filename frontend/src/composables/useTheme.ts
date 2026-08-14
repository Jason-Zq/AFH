import { ref, watch } from 'vue'

/**
 * 明暗主题切换：Element Plus 官方方案 = dark/css-vars + html.dark 类。
 * 模块级单例（全应用共享一份状态）；优先级 localStorage > prefers-color-scheme，默认深色（沿用原 UI）。
 */
const STORAGE_KEY = 'ra-theme'
const stored = localStorage.getItem(STORAGE_KEY)
const isDark = ref(
  stored ? stored === 'dark' : (window.matchMedia?.('(prefers-color-scheme: light)').matches ?? false) === false,
)

watch(isDark, dark => {
  document.documentElement.classList.toggle('dark', dark)
  localStorage.setItem(STORAGE_KEY, dark ? 'dark' : 'light')
}, { immediate: true })

export function useTheme() {
  function toggle(): void {
    isDark.value = !isDark.value
  }
  return { isDark, toggle }
}
