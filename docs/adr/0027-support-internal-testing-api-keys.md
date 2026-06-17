# Support Internal Testing API Keys

StockMountain supports Internal Testing API Keys for team development and app testing in the first version. These keys are not an external developer product surface.

**Consequences**

Internal Testing API Keys should be revocable and scoped, but the first version does not need user-facing external developer key management. The Internal Testing API may be available in production only behind explicit internal credentials, scopes, audit logging, and rate limits. External developer API access can be revisited later if it becomes a product requirement.
