<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Menu } from '@element-plus/icons-vue'
import AppSidebar from './components/AppSidebar.vue'
import { useMediaQuery } from './composables/useMediaQuery'
import { useTheme } from './composables/useTheme'

// 初始化主题副作用（html.dark 类切换）
useTheme()

const isNarrow = useMediaQuery('(max-width: 1100px)')
const drawerOpen = ref(false)

const route = useRoute()
// /admin 走独立后台布局（AdminLayout 自带侧栏与 router-view），此处只透传
const isAdmin = computed(() => route.path.startsWith('/admin'))

// 窄屏下导航后自动收起抽屉
watch(() => route.fullPath, () => { drawerOpen.value = false })
</script>

<template>
  <router-view v-if="isAdmin" />
  <div v-else class="app-shell">
    <AppSidebar v-if="!isNarrow" />
    <template v-else>
      <el-button class="menu-btn" :icon="Menu" circle @click="drawerOpen = true" />
      <el-drawer v-model="drawerOpen" direction="ltr" size="280px" :with-header="false">
        <AppSidebar class="drawer-sidebar" />
      </el-drawer>
    </template>
    <main class="app-main">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.app-shell { display: flex; height: 100vh; }
.app-main { flex: 1; min-width: 0; height: 100vh; overflow: hidden; }
.menu-btn { position: fixed; top: 12px; left: 12px; z-index: 10; }
/* 抽屉内让侧栏撑满（子组件根元素可被父级 scoped 样式命中；双类选择器提优先级） */
.app-shell .drawer-sidebar { width: 100%; height: 100%; position: static; border: none; }
.app-shell :deep(.el-drawer__body) { padding: 0; }
</style>
