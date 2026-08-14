<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { Download } from '@element-plus/icons-vue'

const props = defineProps<{ markdown: string | null; pending?: boolean }>()

interface Heading { text: string; depth: number }

// TOC 只收 h1-h3；纯文本提取（去掉行内 md 记号）
const headings = computed<Heading[]>(() => {
  if (!props.markdown) return []
  return marked.lexer(props.markdown)
    .filter(t => t.type === 'heading' && (t as { depth: number }).depth <= 3)
    .map(t => ({
      text: ((t as { text: string }).text ?? '').replace(/[*_`#]/g, ''),
      depth: (t as { depth: number }).depth,
    }))
})

// 模型输出先 marked 转 HTML，再 DOMPurify 消毒后才允许上屏
const reportHtml = computed(() =>
  props.markdown ? DOMPurify.sanitize(marked.parse(props.markdown, { async: false })) : null,
)

// 渲染后按序给标题补锚点 id，与 TOC 下标一一对应
const body = ref<HTMLElement>()
watch(reportHtml, async () => {
  await nextTick()
  body.value?.querySelectorAll('h1,h2,h3').forEach((h, i) => { h.id = `report-h-${i}` })
})

function jump(i: number): void {
  document.getElementById(`report-h-${i}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function download(): void {
  if (!props.markdown) return
  const url = URL.createObjectURL(new Blob([props.markdown], { type: 'text/markdown;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = '研究报告.md'
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <section class="report-view">
    <el-skeleton v-if="pending" :rows="8" animated class="report-skeleton" />
    <el-empty v-else-if="!reportHtml" description="报告生成后会显示在这里。" />
    <template v-else>
      <div class="report-layout">
        <nav v-if="headings.length >= 3" class="report-toc">
          <div class="toc-title">目录</div>
          <a
            v-for="(h, i) in headings"
            :key="i"
            class="toc-item"
            :class="`depth-${h.depth}`"
            @click="jump(i)"
          >{{ h.text }}</a>
        </nav>
        <div ref="body" class="report-body" v-html="reportHtml"></div>
      </div>
      <el-button :icon="Download" class="download-btn" @click="download">下载 Markdown</el-button>
    </template>
  </section>
</template>

<style scoped>
.report-view { overflow-y: auto; min-height: 0; padding-right: 4px; }
.report-skeleton { background: var(--panel); border: 1px solid var(--panel-border); border-radius: 10px; padding: 20px 24px; }
.report-layout { display: flex; gap: 16px; align-items: flex-start; }
.report-toc {
  position: sticky; top: 0; flex-shrink: 0; width: 180px;
  font-size: 0.82rem; max-height: 100%; overflow-y: auto;
}
.toc-title { color: var(--text-dim); margin-bottom: 6px; font-size: 0.78rem; }
.toc-item {
  display: block; color: var(--text-dim); cursor: pointer; padding: 2px 0;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.toc-item:hover { color: var(--accent); }
.toc-item.depth-2 { padding-left: 12px; }
.toc-item.depth-3 { padding-left: 24px; }
@media (max-width: 768px) { .report-toc { display: none; } }
.download-btn { margin-top: 12px; }

.report-body {
  flex: 1; min-width: 0;
  background: var(--panel); border: 1px solid var(--panel-border);
  border-radius: 10px; padding: 20px 24px; line-height: 1.75; font-size: 0.95rem;
}
/* v-html 内容不受 scoped 属性限制，需要 :deep */
.report-body :deep(h1), .report-body :deep(h2), .report-body :deep(h3) { color: var(--accent); margin: 0.9em 0 0.4em; scroll-margin-top: 12px; }
.report-body :deep(table) { border-collapse: collapse; width: 100%; margin: 12px 0; }
.report-body :deep(th), .report-body :deep(td) { border: 1px solid var(--panel-border); padding: 6px 10px; font-size: 0.88rem; }
.report-body :deep(code) { background: var(--tool-bg); padding: 1px 6px; border-radius: 4px; font-size: 0.88em; }
.report-body :deep(blockquote) { border-left: 3px solid var(--accent); margin: 10px 0; padding: 4px 14px; color: var(--text-dim); }
</style>
