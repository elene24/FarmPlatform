import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development the SPA and API share one origin: Vite proxies /api and /health to the
// ASP.NET Core backend, so session/CSRF cookies are first-party and no CORS is involved.
const apiTarget = process.env.VITE_DEV_API_TARGET ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: apiTarget, changeOrigin: false },
      '/health': { target: apiTarget, changeOrigin: false },
    },
  },
})
