# Defer Webhooks Beyond V1

StockMountain does not support webhooks in the first version. V1 uses in-app Notifications and ordinary backend/API reads for event visibility.

**Consequences**

Webhook endpoint management, signing, retries, delivery history, and replay tooling are deferred beyond V1. The first version should keep event delivery inside StockMountain.
