<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Back, Collection, DataAnalysis, Document, List, Moon, Sunny, Tools } from '@element-plus/icons-vue'
import { useTheme } from '../composables/useTheme'

/** 后台侧边栏：菜单 + 返回前台 + 主题切换。AdminLayout 宽屏直挂、窄屏放抽屉里复用。 */
const route = useRoute()
const router = useRouter()
const { isDark, toggle } = useTheme()

const menus = [
  { path: '/admin/dashboard', title: '系统状态', icon: DataAnalysis },
  { path: '/admin/documents', title: '语料管理', icon: Document },
  { path: '/admin/sessions', title: '会话管理', icon: Collection },
  { path: '/admin/ops', title: '运维操作', icon: Tools },
  { path: '/admin/logs', title: '日志查看', icon: List },
]

const activeMenu = computed(() => route.path)
</script>

<template>
  <aside class="admin-aside">
    <div class="admin-brand">🛠 后台管理</div>
    <el-menu class="admin-menu" :default-active="activeMenu" router>
      <el-menu-item v-for="m in menus" :key="m.path" :index="m.path">
        <el-icon><component :is="m.icon" /></el-icon>
        <span>{{ m.title }}</span>
      </el-menu-item>
    </el-menu>
    <div class="admin-footer">
      <el-button :icon="Back" text @click="router.push('/')">返回前台</el-button>
      <el-button :icon="isDark ? Sunny : Moon" circle @click="toggle" />
    </div>
  </aside>
</template>

<style scoped>
.admin-aside {
  width: 220px;
  flex-shrink: 0;
  height: 100vh;
  position: sticky;
  top: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
}
.admin-brand { padding: 18px 16px 10px; font-weight: 600; font-size: 15px; }
.admin-menu { flex: 1; border-right: none; }
.admin-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color-light);
}
</style>
