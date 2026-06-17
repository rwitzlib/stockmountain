# Use In-App Notifications in V1

StockMountain uses in-app Notifications in the first version for important events such as Backtest Run completion, Backtest Run failure, Paper Bot trade activity, Paper Bot pauses, and Corporate Action Holds.

**Consequences**

Email, SMS, Discord, and webhook notifications can be added later, but the first version should persist notifications inside StockMountain so users can review events in the frontend.

Paper Bot Notifications are controlled by Notification Preferences, with a default of notifying on trade open, trade close, bot paused, and Corporate Action Hold.
