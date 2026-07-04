# Clerk API Auth Migration

StockMountain now provisions app user profiles from Clerk webhooks, but API
authorization still accepts the existing OpenAuth JWTs. Migrate API auth in a
separate deployment so user provisioning and API token validation can be tested
independently.

## Target Flow

1. The React app signs users in with Clerk.
2. API clients request a Clerk session token with `getToken()`.
3. Requests send `Authorization: Bearer <clerk-session-jwt>`.
4. `MarketViewer.Api` validates Clerk JWTs and maps the JWT `sub` claim to
   `AuthContext.UserId`.
5. DynamoDB user lookups use the Clerk user id stored in `UserRecord.Id`.

## Backend Steps

1. Add Clerk auth configuration:
   - `ClerkAuth:Authority` for the Clerk issuer URL.
   - `ClerkAuth:Audience` only if a Clerk JWT template uses an audience.
2. Update JWT bearer validation in `Program.cs` to accept Clerk session JWTs.
   Keep OpenAuth validation during the transition if existing users still rely
   on OpenAuth tokens.
3. Update `AuthContextMiddleware`:
   - Prefer Clerk `sub` as `AuthContext.UserId`.
   - Keep the existing OpenAuth `properties.username` fallback until migration
     is complete.
   - Derive app role from `UserRecord.Role`, not from mutable client claims.
4. Update user-protected handlers to assume `AuthContext.UserId` is the Clerk
   user id.

## Frontend Steps

1. Replace `localStorage.getItem('accessToken')` authorization headers with
   Clerk `getToken()` from `useAuth()`.
2. Remove OpenAuth refresh/exchange logic from the sidebar once API calls use
   Clerk tokens.
3. Keep `/sign-in` and `/sign-up` routes as the user-facing auth entry points.

## Cutover Checks

1. A new Clerk signup creates a DynamoDB user record via webhook.
2. The signed-in user can call `GET api/user/{clerkUserId}` with a Clerk token.
3. Backtest records use Clerk user ids consistently.
4. Existing OpenAuth users are either migrated or explicitly allowed through the
   fallback path until support is removed.
