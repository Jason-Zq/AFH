import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    // Element Plus 按需引入：模板里的 el-* 组件自动注册 + 自动挂样式，
    // ElMessage 等 API 由 AutoImport 兜底；组件样式默认随组件自动导入（importStyle: 'css'）
    AutoImport({ resolvers: [ElementPlusResolver()], dts: 'src/auto-imports.d.ts' }),
    Components({ resolvers: [ElementPlusResolver()], dts: 'src/components.d.ts' }),
  ],
  server: {
    // 开发期把 API 请求代理到后端，免 CORS
    proxy: {
      '/api': 'http://localhost:5199',
    },
  },
  build: {
    // 产物直接输出到后端 wwwroot，单 dotnet run 即可托管完整应用
    outDir: '../src/ResearchAssistant.Web/wwwroot',
    emptyOutDir: false, // 不清空，避免误删后端自带静态文件
  },
})
