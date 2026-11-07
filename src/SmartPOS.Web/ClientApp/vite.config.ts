import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  base: '/react/',
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://localhost:7198',
        changeOrigin: true,
        secure: false
      },
      '/uploads': {
        target: 'https://localhost:7198',
        changeOrigin: true,
        secure: false
      }
    }
  },
  build: {
    outDir: '../wwwroot/react',
    emptyOutDir: true
  }
});
