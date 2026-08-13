import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
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
