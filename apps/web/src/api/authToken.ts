/**
 * Clerk session token access for non-component modules.
 *
 * ClerkProvider (main.tsx) exposes the loaded Clerk instance on window.Clerk.
 * Session tokens are short-lived and refreshed by Clerk automatically, so a fresh
 * token must be requested per API call — never cache one in localStorage.
 *
 * Prefer authFetch() for API calls: it waits for clerk-js to finish loading,
 * attaches the token, and retries once on a 401 after force-refreshing the
 * token (the common failure after the tab was asleep long enough for the
 * session token to expire).
 */

interface ClerkSession {
  getToken(options?: { skipCache?: boolean }): Promise<string | null>;
}

interface ClerkGlobal {
  loaded?: boolean;
  session?: ClerkSession | null;
  user?: unknown;
}

function clerk(): ClerkGlobal | undefined {
  return (window as { Clerk?: ClerkGlobal }).Clerk;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Wait (bounded) for clerk-js to finish loading. Page code can fire API calls
 * before ClerkProvider has bootstrapped — on a cold load or right after tab
 * wake — and asking for a token then would silently produce an
 * unauthenticated request.
 */
async function waitForClerkLoaded(timeoutMs = 5000): Promise<ClerkGlobal | undefined> {
  const deadline = Date.now() + timeoutMs;
  let instance = clerk();
  while (!instance?.loaded && Date.now() < deadline) {
    await sleep(100);
    instance = clerk();
  }
  return instance;
}

export async function getClerkToken(options?: { skipCache?: boolean }): Promise<string | null> {
  const session = (await waitForClerkLoaded())?.session;
  if (!session) {
    return null;
  }

  try {
    return await session.getToken(options);
  } catch {
    // Typically a transient network failure right after machine/tab wake,
    // before connectivity is back. Pause briefly and force one fresh fetch.
    await sleep(750);
    try {
      return await session.getToken({ ...options, skipCache: true });
    } catch {
      return null;
    }
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

/**
 * fetch() with Clerk auth headers attached. On a 401 — a stale token that
 * clerk-js hadn't refreshed yet — it force-refreshes the session token and
 * replays the request once. Request bodies here are always strings, so the
 * replay is safe.
 */
export async function authFetch(input: string | URL, init: RequestInit = {}): Promise<Response> {
  const headers = {
    ...(await getAuthHeaders()),
    ...(init.headers as Record<string, string> | undefined),
  };

  const response = await fetch(input, { ...init, headers });
  if (response.status !== 401) {
    return response;
  }

  const token = await getClerkToken({ skipCache: true });
  if (!token) {
    return response;
  }

  return fetch(input, {
    ...init,
    headers: { ...headers, Authorization: `Bearer ${token}` },
  });
}

/** Synchronous signed-in check; false until Clerk finishes loading. Prefer useUser() in components. */
export function isClerkSignedIn(): boolean {
  return !!clerk()?.user;
}
