# Use Stripe for Billing

StockMountain uses Stripe for Membership billing and purchased Credits. Stripe handles subscription payments, one-time Credit purchases, invoices, and billing webhooks, while StockMountain records Membership state and Credit Ledger entries internally.

**Consequences**

User Accounts should store the Stripe customer relationship without making Stripe the domain source of truth for Credits. Stripe webhooks must be processed idempotently so Membership updates and Credit Grants are recorded exactly once.
