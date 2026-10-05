# AGENTS_ADDENDUM_POSTGRES_ASSETS.md

Merge these rules into the repository AGENTS.md.

## PostgreSQL
- Never hard-code or commit DB passwords/connection strings.
- Never issue SQL from gameplay/domain systems.
- Runtime simulation stays in memory.
- Database writes are transactional and batched at persistence boundaries.
- Preserve stable IDs across DB round-trips.
- Any schema change requires a migration and migration test.

## External assets
- Use only assets with traceable and compatible licenses.
- Record every third-party asset in docs/ASSET_REGISTRY.md before using it beyond staging.
- Never bypass login, CAPTCHA or license acceptance.
- If acquisition is blocked, document the exact manual download in docs/MANUAL_ASSET_DOWNLOADS.md and use a placeholder.
- Normalize scale, pivot, material, collider, rig/avatar, animations and LOD before runtime use.
