# Corporate Actions Pause Affected Paper Bots

Live Paper Bots use current market prices rather than historical split-adjusted bars. If a corporate action such as a stock split could affect an open simulated position, the affected Paper Bot enters Corporate Action Hold instead of automatically adjusting the position in the first version.

**Consequences**

Paper Bot execution needs a way to detect or be notified of relevant corporate actions for open positions. Automatic live position adjustment can be added later, but V1 favors pausing and review over silently changing simulated positions.
