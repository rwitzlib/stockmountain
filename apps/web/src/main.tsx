import { ClerkProvider } from '@clerk/react';
import { shadcn } from '@clerk/ui/themes';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './index.css';

const clerkPublishableKey = import.meta.env.VITE_CLERK_PUBLISHABLE_KEY;

if (!clerkPublishableKey) {
  throw new Error('Missing VITE_CLERK_PUBLISHABLE_KEY');
}

// Set theme based on localStorage or default to dark
const savedTheme = localStorage.getItem('theme');
if (savedTheme === 'light') {
  document.documentElement.classList.remove('dark');
} else {
  document.documentElement.classList.add('dark');
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ClerkProvider
      afterSignOutUrl="/"
      appearance={{
        theme: shadcn,
        // The shadcn theme expects shadcn v4 variables holding complete colors
        // (e.g. --card: #fff). This app uses the v3 convention with raw HSL
        // channels (--card: 0 0% 100%), so wrap everything in hsl() here.
        variables: {
          colorBackground: 'hsl(var(--card))',
          colorForeground: 'hsl(var(--card-foreground))',
          colorPrimary: 'hsl(var(--primary))',
          colorPrimaryForeground: 'hsl(var(--primary-foreground))',
          colorDanger: 'hsl(var(--destructive))',
          colorNeutral: 'hsl(var(--foreground))',
          colorMuted: 'hsl(var(--muted))',
          colorMutedForeground: 'hsl(var(--muted-foreground))',
          colorInput: 'hsl(var(--input))',
          colorInputForeground: 'hsl(var(--card-foreground))',
          colorBorder: 'hsl(var(--border))',
          colorRing: 'hsl(var(--ring) / 0.5)',
          colorModalBackdrop: 'rgba(0, 0, 0, 0.5)',
          // The theme points these at Tailwind v4 --font-weight-* variables,
          // which don't exist in this Tailwind v3 project.
          fontWeight: { normal: 400, medium: 500, semibold: 600, bold: 600 },
        },
      }}
      publishableKey={clerkPublishableKey}
    >
      <App />
    </ClerkProvider>
  </StrictMode>
);