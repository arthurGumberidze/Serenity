# DECISIONS_ADDENDUM_POSTGRES_ASSETS.md

Append to docs/DECISIONS.md:

### D-006 PostgreSQL persistence
The development PC PostgreSQL instance is the primary durable world/history provider for the current prototype.
Active simulation remains in RAM. Gameplay depends on persistence interfaces, not PostgreSQL directly.
Credentials are never committed. Schema is migration-versioned. Provider must remain replaceable.

### D-007 External art strategy
MVP art uses free, legally compatible third-party assets and placeholders.
Every external asset is tracked in docs/ASSET_REGISTRY.md.
A standardized import/normalization pipeline is mandatory.
Blocked downloads must not block development; use placeholders and document manual acquisition.
