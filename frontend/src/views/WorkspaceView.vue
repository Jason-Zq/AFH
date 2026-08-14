<script setup lang="ts">
import { useResearchStore } from '../stores/research'
import { useMediaQuery } from '../composables/useMediaQuery'
import StageStepper from '../components/StageStepper.vue'
import ActivityStream from '../components/ActivityStream.vue'
import ReportView from '../components/ReportView.vue'

const research = useResearchStore()
const isNarrow = useMediaQuery('(max-width: 1100px)')
</script>

<template>
  <div class="workspace">
    <header class="ws-header">
      <h2 class="ws-question" :title="research.question">{{ research.question || '新研究' }}</h2>
      <el-button v-if="research.running" type="danger" plain size="small" @click="research.cancel">取消</el-button>
    </header>

    <StageStepper :blocks="research.blocks" />

    <el-alert v-if="research.error" :title="research.error" type="error" :closable="false" class="ws-banner" />
    <el-alert v-else-if="research.notice" :title="research.notice" type="info" :closable="false" class="ws-banner" />

    <!-- 窄屏：双栏变 Tab；宽屏：左过程右报告 -->
    <el-tabs v-if="isNarrow" class="ws-tabs">
      <el-tab-pane label="协作过程"><ActivityStream /></el-tab-pane>
      <el-tab-pane label="研究报告">
        <ReportView :markdown="research.reportMarkdown" :pending="research.running && !research.reportMarkdown" />
      </el-tab-pane>
    </el-tabs>
    <div v-else class="ws-panes">
      <ActivityStream />
      <ReportView :markdown="research.reportMarkdown" :pending="research.running && !research.reportMarkdown" />
    </div>
  </div>
</template>

<style scoped>
.workspace { height: 100%; display: flex; flex-direction: column; padding: 20px 24px; min-height: 0; }
.ws-header { display: flex; align-items: center; gap: 12px; margin-bottom: 6px; }
.ws-question {
  margin: 0; font-size: 1.15rem; flex: 1; min-width: 0;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.ws-banner { margin-bottom: 10px; }
.ws-panes {
  flex: 1; min-height: 0; display: grid;
  grid-template-columns: 1.2fr 1fr; gap: 18px;
}
.ws-tabs { flex: 1; min-height: 0; display: flex; flex-direction: column; }
.ws-tabs :deep(.el-tabs__content) { flex: 1; min-height: 0; }
.ws-tabs :deep(.el-tab-pane) { height: 100%; display: flex; flex-direction: column; }
@media (max-width: 1100px) {
  .workspace { padding: 14px 14px 14px 56px; } /* 左侧给抽屉按钮让位 */
}
</style>
