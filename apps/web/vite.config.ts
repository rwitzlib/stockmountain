import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig(() => ({
  server: {
    host: "::",
    port: 8080,
    allowedHosts: ['stockmountain.io'],
    hmr: {
      overlay: false
    }
  },
  plugins: [
    react(),
  ].filter(Boolean),
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  optimizeDeps: {
    exclude: ['lucide-react'],
  },
}));