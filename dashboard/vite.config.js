import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  define: {
    __DEFAULT_SERVER_URL__: JSON.stringify(process.env.VERBEX_SERVER_URL || 'http://localhost:8080'),
    __DEFAULT_API_KEY__: JSON.stringify(process.env.VERBEX_API_KEY || 'verbexadmin')
  },
  server: {
    host: '0.0.0.0',
    port: 8200
  }
})
