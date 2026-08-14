<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Menu } from '@element-plus/icons-vue'
import AdminAside from '../components/AdminAside.vue'
import { useMediaQuery } from '../composables/useMediaQuery'

/** 后台独立布局（vue-element-admin 式：侧菜单 + 内容区），与前台 App 壳互斥（App.vue 按路由切换）。 */
const isNarrow = useMediaQuery('(max-width: 1100px)')
const drawerOpen = ref(false)

// 窄屏下导航后自动收起抽屉
const route = useRoute()
watch(() => route.fullPath, () => { drawerOpen.value = false })
</script>

<template>
  <div class="admin-shell">
    <AdminAside v-if="!isNarrow" />
    <template v-else>
      <el-button class="menu-btn" :icon="Menu" circle @click="drawerOpen = true" />
      <el-drawer v-model="drawerOpen" direction="ltr" size="260px" :with-header="false">
        <AdminAside class="drawer-aside" />
      </el-drawer>
    </template>
    <main class="admin-main">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.admin-shell { display: flex; min-height: 100vh; }
.admin-main { flex: 1; min-width: 0; padding: 20px 24px; }
.menu-btn { position: fixed; top: 12px; left: 12px; z-index: 10; }
/* 抽屉内让侧栏撑满（子组件根元素可被父级 scoped 样式命中；双类选择器提优先级） */
.admin-shell .drawer-aside { width: 100%; height: 100%; position: static; border: none; }
.admin-shell :deep(.el-drawer__body) { padding: 0; }
</style>
