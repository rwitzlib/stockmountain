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
      appearance={{ theme: shadcn }}
      publishableKey={clerkPublishableKey}
    >
      <App />
    </ClerkProvider>
  </StrictMode>
);