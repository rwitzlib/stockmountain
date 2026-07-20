# 07 — Share backtest via public link

Self-contained plan (written to be handed to a fresh chat instance with no other context).

## Feature summary

A **Share** button in the top-right of the backtest detail page mints a public, unguessable URL
(`https://stockmountain.io/share/{shareId}`) that anyone — including people with no account and no
auth token — can open to see a **fully interactive replica** of the backtest results: equity curve
with SPY benchmark overlay, stats, histograms, exit-reason panel, trades table. The link works
because the backend bakes everything the page needs into a single JSON payload at share time;
the anonymous viewer never touches an authed API.

## Decisions (settled during design grilling — do not relitigate)

| Question | Decision |
|---|---|
| Viewer experience | Full interactive replica reusing existing React components (not a screenshot, not a cut-down summary) |
| Strategy config (entry filters / exit settings = the owner's IP) | Owner toggles per share. When hidden, redaction is **server-side**: payload carries only counts (`entryFilterCount`, which exit types exist). UI renders locked/blurred teaser panels from the counts. The real values must never be present in the public payload. |
| Live-data gaps | SPY daily series for the backtest window is baked into the payload (few KB) so the benchmark overlay works. Per-trade candle drill-downs are absent in share view. |
| Delivery | Public SPA route `/share/:shareId` + anonymous JSON endpoint. No new hosting infra. |
| Serving path | `GET /api/share/{shareId}` — unauthenticated route on the existing API, reading the baked payload from the private `backtest_data` bucket under a `shares/` prefix. |
| Expiry | Fixed 30 days via an S3 lifecycle rule on the `shares/` prefix. Expired link → friendly "this share has expired" page with sign-up CTA. No owner-facing revocation in v1. |
| Re-share | Every click mints a **new** shareId; old links keep their own expiry clocks. (Lets the owner share masked to one audience and unmasked to another.) |
| Tracking | None. S3 object existence = share is live. No DynamoDB record, no view counts, no "my shares" page. |
| Link previews (OG tags) | Punt. Generic site card in v1; rich crawler previews are a fast-follow enabled by the API serving path. |

## Existing landmarks

- **API**: `apps/api/MarketViewer.Api/Controllers/Market/BacktestController.cs` — routes
  `backtest`, `backtest/{id}`, `backtest/result/{id}`, `backtest/universe/{id}`. Auth is Clerk JWT;
  `AuthContextMiddleware` (`apps/api/MarketViewer.Api/Middleware/AuthContextMiddleware.cs`)
  tolerates anonymous requests (it just leaves `AuthContext.IsAuthenticated` false), so an
  anonymous endpoint mainly needs to skip the authenticated-user checks — verify how existing
  endpoints reject anonymous callers (`[Authorize]` vs. explicit `authContext` checks) and mirror
  the opposite.
- **Storage**: `packages/marketviewer-infrastructure/MarketViewer.Infrastructure/Services/BacktestRepository.cs`
  — DynamoDB record + S3 objects at `backtestResults/{userId}/{id}/portfolio.json` in the
  `backtest_data` bucket (`infra/tf/app/s3.tf`, bucket stays fully private).
- **Web**: `apps/web/src/pages/BacktestDetailPage.tsx` (detail page; header is where the Share
  button goes), `apps/web/src/api/backtestApi.ts` (authed fetch wrappers),
  `apps/web/src/routes/index.tsx` (routes are not auth-gated client-side),
  `apps/web/src/services/massive.ts` (`fetchMarketData` — used today for the SPY overlay).

## Contracts

New file in `packages/marketviewer-contracts` (mirror as TS types in `apps/web/src/types/`):

```csharp
public class BacktestSharePayload
{
    public int SchemaVersion { get; set; }          // start at 1; SPA refuses versions it doesn't know
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }   // CreatedAt + 30d; SPA shows "expires in N days"
    public string Title { get; set; }               // backtest name only — NO userId, NO email
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public BacktestShareConfig Config { get; set; } // exactly one of the two branches below
    public TradingData Result { get; set; }         // same shape the detail page already renders
    public List<SharedBenchmarkPoint> Benchmark { get; set; } // SPY daily closes for the window
}

public class BacktestShareConfig
{
    public bool Masked { get; set; }
    // Masked == false: full position/entry/exit settings (same shapes the detail page uses)
    // Masked == true:  ONLY these counts — the branches are mutually exclusive
    public int? EntryFilterCount { get; set; }
    public bool? HasStopLoss { get; set; }
    public bool? HasProfitTarget { get; set; }
    public bool? HasTimedExit { get; set; }
}
```

**Hard rule**: redaction happens when the payload is *composed*. Never write the full config to S3
and mask in the UI — the payload is public; DevTools is the test.

**PII rule**: the payload must not contain `userId`, Clerk IDs, or anything traceable to the owner.
The share is anonymous-by-default.

## Backend

1. **Create share** — `POST backtest/{id}/share`, authed, on `BacktestController`.
   Body: `{ "includeConfig": bool }`.
   - Load the `BacktestContextRecord`; verify `record.UserId == authContext.UserId` (404 otherwise,
     matching existing not-your-backtest behavior). Reject if the backtest isn't completed.
   - Load the portfolio result via `BacktestRepository.GetPortfolioFromS3`.
   - Fetch SPY daily bars for `[start, end]`.
   - Compose `BacktestSharePayload` (redact per `includeConfig`).
   - `shareId` = 128+ bits of crypto randomness, URL-safe (e.g. 22-char base64url of a GUID, or
     32-hex). Never derived from the backtest ID.
   - Write to `shares/{shareId}.json` in the `backtest_data` bucket.
   - Return `{ "shareId": "...", "url": "https://stockmountain.io/share/{shareId}", "expiresAt": "..." }`.

2. **Read share** — `GET share/{shareId}`, anonymous, in a new
   `Controllers/Market/ShareController.cs`.
   - Validate `shareId` against `^[A-Za-z0-9_-]{20,64}$` before touching S3 (no path traversal,
     no junk keys).
   - S3 `NoSuchKey` → 404 (SPA renders the expired/not-found page; don't distinguish expired vs.
     never-existed — that leaks nothing and needs no metadata).
   - Return the JSON body as-is with `Cache-Control: public, max-age=3600` (payloads are immutable;
     re-share = new ID, so caching is safe).
   - This endpoint serves anonymous internet traffic: confirm the API's rate limiting / request
     size posture covers it; at minimum ensure the route can't be used to enumerate (random 404s
     are cheap, but log a metric on 404 rate).

3. **Repository** — add `PutShare(string shareId, string json)` / `GetShare(string shareId)` to
   `BacktestRepository` (or a small `ShareRepository`) using the existing S3 client and bucket
   config.

## Infra (Terraform)

In `infra/tf/app/s3.tf`, add to the `backtest_data` bucket:

```hcl
resource "aws_s3_bucket_lifecycle_configuration" "backtest_data" {
  bucket = aws_s3_bucket.backtest_data.id
  rule {
    id     = "expire-shares"
    status = "Enabled"
    filter { prefix = "shares/" }
    expiration { days = 30 }
  }
}
```

Note: S3 lifecycle expiry runs on a daily sweep, so links may live up to ~48h past the 30-day
mark. `ExpiresAt` in the payload is the authoritative display value; if exact cutoff matters
later, the GET endpoint can also 404 when `ExpiresAt < now`. Bucket stays fully private — the
API is the only reader.

If a lifecycle configuration already exists for this bucket, add the rule to it (a bucket has
exactly one lifecycle configuration resource).

## Web

1. **Share dialog** — new `apps/web/src/components/backtest/ShareDialog.tsx` (Radix dialog, matching
   existing dialog styling). Trigger: Share button (lucide `Share2`) in the `BacktestDetailPage`
   header, top-right, enabled only when results are loaded. Contents:
   - Toggle: "Include strategy configuration" (default **off** — private by default).
   - Create → `backtestApi.createShare(id, includeConfig)` → show the URL with a copy button and
     "expires in 30 days". Each click of Create mints a fresh link; copy in the dialog states this.

2. **Public route** — add `/share/:shareId` to `apps/web/src/routes/index.tsx` → new
   `apps/web/src/pages/SharedBacktestPage.tsx`:
   - Fetches `GET /api/share/{shareId}` **without** auth headers (plain `fetch`, not the authed
     wrappers — the page must work signed-out; verify nothing in the app shell forces a Clerk
     sign-in on this route).
   - 404 → "This shared backtest has expired or doesn't exist" + sign-up CTA.
   - Unknown `schemaVersion` → same friendly error (payloads written by an older API after an SPA
     redesign should degrade politely, not crash).
   - Renders the same components the detail page uses (`EquityCurveCard`, `DailyPnlChart`,
     `HistogramChart`, `ExitReasonPanel`, `TickerLeadersPanel`, `BacktestTradesTable`, …) fed from
     the payload instead of hooks. Expect light refactoring: components must accept data via props
     rather than fetching; keep the detail page and share page consuming the same prop shapes.
   - `Config.Masked` → locked teaser panels: "4 entry filters · stop loss · timed exit — hidden by
     owner", blurred placeholder styling, sign-up CTA.
   - No per-trade drill-downs; benchmark overlay reads the baked series.
   - Banner: "Shared backtest · read-only · expires {date}" + prominent "Try StockMountain" CTA
     (this page is a marketing surface).

3. **API client** — `createShare` in `apps/web/src/api/backtestApi.ts` (authed); the share GET
   lives in the share page (unauthed).

## Ordering & compatibility

1. Contracts → backend endpoints + repository → Terraform lifecycle rule → web dialog → web share
   page. The share page is the bulk of the work (component prop-ification).
2. Payloads are written once and never migrated; every SPA change to the share page must keep
   rendering `SchemaVersion: 1` payloads for their 30-day lifetime or show the friendly error.

## Test checklist

- Share an owned, completed backtest → URL opens in an incognito window with no Clerk session:
  full stats, equity curve with SPY overlay, trades table.
- Masked share → public JSON (view the network response!) contains counts only — grep the raw
  payload for a known filter value and confirm absence.
- Unmasked share → config panels render as on the detail page.
- Share someone else's backtest ID / an incomplete backtest → 404 / 4xx.
- `GET /api/share/does-not-exist` and a malformed ID (`../"`, 200 chars) → clean 404, no S3 error
  leak.
- Two Create clicks → two distinct working URLs.
- Delete the S3 object manually → link shows the expired page.
- Detail page behavior unchanged for signed-in users (prop-ification refactor is invisible).
