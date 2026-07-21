// API origin is configurable per environment via Vite env var (baked at build time):
// set VITE_API_URL=https://dev.stockmountain.io in the dev build. Defaults to production.
export const API_ORIGIN: string = import.meta.env.VITE_API_URL ?? 'https://stockmountain.io';
export const API_BASE_URL = `${API_ORIGIN}/api`;
