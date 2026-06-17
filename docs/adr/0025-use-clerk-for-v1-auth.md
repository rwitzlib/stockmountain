# Use Clerk for V1 Authentication

StockMountain uses Clerk as the third-party authentication provider for the first version. Clerk fits the initial React frontend and .NET backend needs while avoiding custom authentication implementation.

**Consequences**

StockMountain should keep provider-neutral User Account ownership inside its own database rather than treating Clerk identifiers as the full domain model. This preserves a path to support additional auth requirements or provider migration later.
