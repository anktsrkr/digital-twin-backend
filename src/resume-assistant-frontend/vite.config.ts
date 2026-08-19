import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  base: process.env.GITHUB_PAGES === 'true' ? '/resume-assistant/' : '/',
  server: {
    port: 5173,
    host: true,
    proxy: {
      // Proxy Supabase Auth calls to local GoTrue container to eliminate CORS issues
      '/auth/v1': {
        target: 'http://localhost:9999',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/auth\/v1/, '')
      }
    }
  }
});
