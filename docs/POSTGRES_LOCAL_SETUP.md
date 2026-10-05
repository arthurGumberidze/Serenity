# POSTGRES_LOCAL_SETUP.md

## Purpose
Local PostgreSQL is used as the durable persistence/history provider for development.

## Security
Do not place passwords or full connection strings in Git.
Use environment variables or a local ignored file.

Recommended environment variables:
- DYNASTYGAME_PG_HOST
- DYNASTYGAME_PG_PORT
- DYNASTYGAME_PG_DATABASE
- DYNASTYGAME_PG_USER
- DYNASTYGAME_PG_PASSWORD
- DYNASTYGAME_PG_TEST_DATABASE

## Database separation
Use separate development and automated-test databases.

Suggested names:
- dynasty_game_dev
- dynasty_game_test

## Architectural rule
Unity gameplay/domain assemblies depend on persistence interfaces only.
The PostgreSQL client and SQL/migrations live in Infrastructure/Persistence.Postgres.
