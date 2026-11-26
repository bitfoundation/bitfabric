#!/usr/bin/env bash
# Step 1 of carrying an environment's data into the model of bitfoundation/bitfabric#73 (bitfabric-internal/specs/changes/
# 2026-11-26-teams-licenses-projects.md, The data script). Runs inside the environment's postgresserver container, after
# bitfabric-internal's fabric-database-remodel.yml has kept a dump of the database on the server:
#
#   docker exec -i <postgresserver> bash -s < save.sh
#
# It moves the old tables aside, public into the schema "legacy" and Hangfire's jobs into "legacy_jobs", and leaves public
# empty, so the next deployment's migrations build the new model there from its one Initial. carry-over.sql then copies
# the rows across. The apps that run until that deployment fail, as their tables are gone.
set -Eeuo pipefail

export PGPASSWORD="$POSTGRES_PASSWORD"
user="${POSTGRES_USER:-postgres}"
database="${POSTGRES_DB:-postgresdb}"
sql() { psql -h 127.0.0.1 -U "$user" -d "$database" -X -v ON_ERROR_STOP=1 -Atq "$@"; }

if [[ "$(sql -c "select count(*) from pg_namespace where nspname = 'legacy'")" != "0" ]]; then
    echo "save.sh: the schema \"legacy\" is there already: the data was saved before and not carried over yet. Run carry-over.sql, or roll back." >&2
    exit 1
fi

if [[ "$(sql -c "select count(*) from information_schema.tables where table_schema = 'public' and table_name = 'LicensedProjects'")" != "1" ]]; then
    echo "save.sh: this database has no \"LicensedProjects\" table, so it isn't on the model before bitfoundation/bitfabric#73. Nothing is moved." >&2
    exit 1
fi

echo "Each table's rows, before:"
printf '%s \\gexec\n' "select format('select %L || '' '' || count(*) from %I.%I', n.nspname || '.' || c.relname, n.nspname, c.relname) from pg_class c join pg_namespace n on n.oid = c.relnamespace where c.relkind in ('r', 'p') and n.nspname in ('public', 'jobs') order by 1" \
    | sql -f -

sql <<'SQL'
BEGIN;
ALTER SCHEMA public RENAME TO legacy;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO public;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'jobs') THEN
        ALTER SCHEMA jobs RENAME TO legacy_jobs;
    END IF;
END $$;
COMMIT;
SQL

echo "save.sh: the old tables are in \"legacy\" and \"legacy_jobs\", and public is empty. Deploy the new version now, then run carry-over.sql."
