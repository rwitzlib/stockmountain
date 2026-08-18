import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig(() => ({
  server: {
    host: "::",
    port: 5173,
    allowedHosts: ['dev.stockmountain.io'],
    hmr: {
      overlay: false
    },
    fs: {
      // docs/filters/*.md lives at the repo root and is imported via import.meta.glob.
      allow: [path.resolve(__dirname, '../..')],
    },
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