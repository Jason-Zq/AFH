<script setup lang="ts">
import { computed } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { useResearch } from '../lib/researchStore'

const { state } = useResearch()

// 模型输出先 marked 转 HTML，再 DOMPurify 消毒后才允许上屏
const reportHtml = computed(() =>
  state.reportMarkdown ? DOMPurify.sanitize(marked.parse(state.reportMarkdown, { async: false })) : null,
)

function download(): void {
  if (!state.reportMarkdown) return
  const url = URL.createObjectURL(new Blob([state.reportMarkdown], { type: 'text/markdown;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = '研究报告.md'
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <section class="report">
    <h2>研究报告</h2>
    <template v-if="reportHtml">
      <div class="report-body" v-html="reportHtml"></div>
      <button class="ghost" @click="download">下载 Markdown</button>
    </template>
    <p v-else class="placeholder">报告生成后会显示在这里。</p>
  </section>
</template>
