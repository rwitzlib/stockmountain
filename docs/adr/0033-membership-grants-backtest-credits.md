# Membership Grants Backtest Credits

Membership includes a recurring allowance of Backtest Run Credits, and users may purchase additional Credits when they need more execution capacity. Monthly Membership Credits expire at the end of the billing period, while purchased Credits do not expire. Membership also covers a defined amount of active Paper Bot capacity. Active Membership is required for Backtest Run execution and Paper Bots, so purchased Credits are add-ons rather than standalone access. Inactive Membership keeps prior data readable but prevents new Backtest Run execution and Paper Bot operation. Paper Bots are paused when Membership becomes inactive, after any billing grace period has elapsed.

**Consequences**

The Credit Ledger must support Credit Grants from both recurring Membership allowances and additional purchases, including expiration for Monthly Credit Grants. Credit consumption spends earliest-expiring Credits first before non-expiring purchased Credits. Credit consumption remains tied to Backtest Run execution rather than Paper Bot runtime or Backfill.
