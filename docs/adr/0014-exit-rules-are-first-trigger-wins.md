# Exit Rules Are First Trigger Wins

When a Strategy has multiple Exit Rules, the first Exit Rule to trigger closes the Trade. The Trade records the Exit Reason so Backtest Run and Paper Bot results can explain why the position closed.

**Consequences**

The Strategy engine must evaluate Exit Rules in a deterministic way when multiple rules trigger at the same evaluation time. Strategies use Exit Rule Priority for ties, with the default priority of Stop Loss, Take Profit, Conditional Exit, then Timed Exit. For bar-based Long Trade evaluation, if Stop Loss and Take Profit are both touched inside the same Bar, Stop Loss wins by default as the conservative assumption. Results should expose the Exit Reason for analysis.
