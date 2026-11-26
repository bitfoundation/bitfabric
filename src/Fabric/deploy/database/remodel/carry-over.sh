#!/usr/bin/env bash
# Step 2 of carrying an environment's data into the model of bitfoundation/bitfabric#73, once save.sh moved the old tables
# into "legacy" and the new version's deployment built the new model in public. Runs inside the postgresserver container,
# with carry-over.sql beside it:
#
#   docker cp carry-over.sql <postgresserver>:/tmp/carry-over.sql
#   docker exec -i <postgresserver> bash -s < carry-over.sh
#
# carry-over.sql copies every row in one transaction and stops, changing nothing, when a count doesn't add up; its report
# lists every table with its rows before and after. Once it has committed, the old schemas are renamed with the time, kept
# for reference and dropped by a person later; the dump save took stays the way back (fabric-database-remodel.yml's rollback).
set -Eeuo pipefail

export PGPASSWORD="$POSTGRES_PASSWORD"
user="${POSTGRES_USER:-postgres}"
database="${POSTGRES_DB:-postgresdb}"
sql() { psql -h 127.0.0.1 -U "$user" -d "$database" -X -v ON_ERROR_STOP=1 "$@"; }

if [[ "$(sql -Atq -c "select count(*) from information_schema.tables where table_schema = 'public' and table_name = 'Monitorings'")" != "1" ]]; then
    echo "carry-over.sh: public has no \"Monitorings\" table yet. Deploy the new version first, so its migrations build the new model." >&2
    exit 1
fi

sql -f /tmp/carry-over.sql

stamp="$(date -u +%Y%m%d%H%M)"
sql -Atq <<SQL
BEGIN;
ALTER SCHEMA legacy RENAME TO legacy_carried_$stamp;
DO \$\$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'legacy_jobs') THEN
        ALTER SCHEMA legacy_jobs RENAME TO legacy_jobs_carried_$stamp;
    END IF;
END \$\$;
COMMIT;
SQL

echo "carry-over.sh: carried. The old tables stay in legacy_carried_$stamp until a person drops them: DROP SCHEMA legacy_carried_$stamp CASCADE."
