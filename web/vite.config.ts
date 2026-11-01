import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Dev proxy: the API (REST + SignalR hub) runs on localhost:8080.
// In the Docker build, nginx (web/nginx.conf) provides the same /api and /hubs proxying.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:8080', changeOrigin: true },
      '/hubs': { target: 'http://localhost:8080', changeOrigin: true, ws: true },
    },
  },
})
