export const config = {
  massive: {
    apiKey: import.meta.env.VITE_MASSIVE_API_KEY || 'DEMO'
  }
} as const;