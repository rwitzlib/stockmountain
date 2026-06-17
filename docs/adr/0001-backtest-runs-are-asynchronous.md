# Backtest Runs Are Asynchronous

All Backtest Runs are submitted and executed asynchronously, including small runs that might complete quickly. This keeps the API contract consistent and avoids maintaining separate inline and background execution paths as intraday and large-universe backtests grow in cost.

**Considered Options**

- Execute small Backtest Runs synchronously and large Backtest Runs asynchronously.
- Execute every Backtest Run asynchronously.

**Consequences**

Clients must create a Backtest Run, observe its status, and retrieve results after execution. The frontend can still provide a fast experience by subscribing to status changes and showing partial progress.
