# Live Consumers Use Normalized Bars

The Live Feed Connector publishes live Normalized Bars as the primary internal market data stream. Strategy evaluation uses Completed Bars by default, while a Strategy may explicitly opt into evaluating In-Progress Bars.

**Consequences**

Raw provider events stay inside the Live Feed Connector boundary unless a future requirement promotes them to a broader contract. Consumers such as live charting, Paper Bot execution, and the Backend API receive the same canonical bar shape, but must distinguish In-Progress Bars from Completed Bars.
