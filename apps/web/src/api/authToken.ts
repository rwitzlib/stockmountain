/**
 * Clerk session token access for non-component modules.
 *
 * ClerkProvider (main.tsx) exposes the loaded Clerk instance on window.Clerk.
 * Session tokens are short-lived and refreshed by Clerk automatically, so a fresh
 * token must be requested per API call — never cache one in localStorage.
 */

interface ClerkGlobal {
  session?: {
    getToken(): Promise<string | null>;
  } | null;
  user?: unknown;
}

function clerk(): ClerkGlobal | undefined {
  return (window as { Clerk?: ClerkGlobal }).Clerk;
}

export async function getClerkToken(): Promise<string | null> {
  const session = clerk()?.session;
  if (!session) {
    return null;
  }

  try {
    return await session.getToken();
  } catch {
    return null;
  }
}

export async function getAuthHeaders(): Promise<Record<string, string>> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  const token = await getClerkToken();
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  return headers;
}

/** Synchronous signed-in check; false until Clerk finishes loading. Prefer useUser() in components. */
export function isClerkSignedIn(): boolean {
  return !!clerk()?.user;
}
