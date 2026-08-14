import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import WorkspaceView from '../views/WorkspaceView.vue'
import SessionView from '../views/SessionView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/run', name: 'run', component: WorkspaceView },
    { path: '/sessions/:id', name: 'session', component: SessionView },
    {
      // 后台整组动态导入：布局独立（App.vue 按 /admin 前缀切换壳），前台首屏零成本
      path: '/admin',
      component: () => import('../layouts/AdminLayout.vue'),
      children: [
        { path: '', redirect: '/admin/dashboard' },
        { path: 'dashboard', name: 'admin-dashboard', component: () => import('../views/admin/AdminDashboard.vue') },
        { path: 'documents', name: 'admin-documents', component: () => import('../views/admin/AdminDocuments.vue') },
        { path: 'sessions', name: 'admin-sessions', component: () => import('../views/admin/AdminSessions.vue') },
        { path: 'ops', name: 'admin-ops', component: () => import('../views/admin/AdminOps.vue') },
        { path: 'logs', name: 'admin-logs', component: () => import('../views/admin/AdminLogs.vue') },
      ],
    },
  ],
})
