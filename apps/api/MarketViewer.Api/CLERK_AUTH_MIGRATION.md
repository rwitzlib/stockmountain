# Clerk API Auth

StockMountain authenticates API requests with Clerk session JWTs and authorizes them
from the DynamoDB user record. OpenAuth support has been removed (hard cutover).

## Design decisions

1. **Token proves identity only.** The Clerk JWT's `sub` claim is the user id. Roles are
   never read from token claims.
2. **DynamoDB is the source of truth for authorization.** `AuthContextMiddleware` loads
   the `UserRecord` on every request (no cache), so role changes — e.g. a future Stripe
   webhook — take effect on the next request.
3. **Roles are purchase tiers, hierarchical.** `UserRole` is `Basic < Advanced < Premium`.
   Endpoints declare a minimum with `[RequiresTier(UserRole.X)]`; higher tiers pass.
4. **Admin is a flag, not a tier.** `UserRecord.IsAdmin` is granted manually and is never
   written by subscription logic, so a lapsed subscription can't demote an admin and a
   purchase can't grant admin. Admin-only endpoints use `[RequiresAdmin]`; admins also
   satisfy every tier requirement.
5. **Lazy provisioning closes the signup race.** If a valid token arrives before the
   `user.created` webhook lands, the middleware provisions the Basic user record inline
   (`Provision` is idempotent via `if_not_exists`). The webhook is a backfill, not a
   dependency.
6. **No anonymous endpoints.** Every controller action requires at least a valid Clerk
   token and the Basic tier. `MarketDataController`, `TickersController`, snapshot,
   performance, and most tools endpoints require admin.

## Request flow

1. React app signs users in with Clerk; `getAuthHeaders()` (`apps/web/src/api/authToken.ts`)
   fetches a fresh session token per request via `window.Clerk.session.getToken()`.
2. JWT bearer middleware validates issuer/lifetime/signature against the Clerk JWKS
   (`ClerkAuth:Authority`, OIDC discovery). Audience is not validated — Clerk session
   tokens carry none — but `azp` is checked against `ClerkAuth:AuthorizedParties`
   when configured.
3. `AuthContextMiddleware` maps `sub` → DynamoDB `UserRecord` (provisioning it if
   missing) and populates the scoped `AuthContext` (`UserId`, `Role`, `IsAdmin`) plus
   `HttpContext.Items["UserId"]`.
4. `TierAuthorizationHandler` / `AdminAuthorizationHandler` enforce
   `[RequiresTier(...)]` / `[RequiresAdmin]` from `AuthContext`.

## Configuration

```json
"ClerkAuth": {
  "Authority": "https://relaxing-koala-79.clerk.accounts.dev",
  "AuthorizedParties": ["http://localhost:5173"]
}
```

- `Authority`: the Clerk instance issuer. Production needs its own instance domain
  (set `ClerkAuth__Authority` env var or a prod appsettings entry).
- `AuthorizedParties`: frontend origins allowed in the token's `azp` claim. Empty
  array skips the check — set this in production.

## Data notes

`Role` must be one of `Basic`, `Advanced`, `Premium`; `IsAdmin` is a separate boolean
attribute (missing = false). Records with pre-tier role strings (`"Admin"`, `"None"`)
are not supported — there are none left in the user store.

## Stripe integration (future)

Subscription webhooks should write only `UserRecord.Role` (mapping plan → tier) and
must never touch `IsAdmin`. Decide downgrade timing (immediately vs. period end)
inside the webhook handler — authorization picks the change up on the next request
either way.
