#!/usr/bin/env bash
# Proves the newest backup is a real one: restores it into a throwaway database, off the server, and checks what comes
# back (bitfabric-internal/specs/build-and-ship.md). A backup that stopped, shrank or doesn't decrypt fails this, and GitHub emails the failure.
set -euo pipefail

: "${BACKUP_AGE_IDENTITY:?}" "${BACKUP_BUCKET:?}"
max_age_hours=${BACKUP_MAX_AGE_HOURS:-7}
min_tables=${BACKUP_MIN_TABLES:-20}
engine=${CONTAINER_ENGINE:-docker}
name=backup-restore-test-$$
work=$(mktemp -d)
trap '$engine rm -f "$name" >/dev/null 2>&1 || true; rm -rf "$work"' EXIT

listing=$(rclone lsf --format "sp" --separator " " --files-only "backup:$BACKUP_BUCKET/db/" | sort -k2)
newest=$(printf '%s\n' "$listing" | tail -n 1 | cut -d' ' -f2)
newest_size=$(printf '%s\n' "$listing" | tail -n 1 | cut -d' ' -f1)
previous_size=$(printf '%s\n' "$listing" | tail -n 2 | head -n 1 | cut -d' ' -f1)

if [ -z "$newest" ]; then
    echo "::error::The bucket has no database backup"
    exit 1
fi

stamp=${newest%.dump.age}
taken=$(date -u -d "${stamp:0:10} ${stamp:11:2}:${stamp:13:2}" +%s)
age_hours=$(( ($(date -u +%s) - taken) / 3600 ))
echo "Newest backup: $newest, $newest_size bytes, $age_hours hours old"

if [ "$age_hours" -ge "$max_age_hours" ]; then
    echo "::error::The newest backup is $age_hours hours old"
    exit 1
fi

if [ "$((newest_size * 2))" -lt "$previous_size" ]; then
    echo "::error::The newest backup is less than half the size of the one before it"
    exit 1
fi

rclone copyto "backup:$BACKUP_BUCKET/db/$newest" "$work/db.dump.age"
(umask 077; printf '%s\n' "$BACKUP_AGE_IDENTITY" > "$work/identity")
age -d -i "$work/identity" -o "$work/db.dump" "$work/db.dump.age"
rm -f "$work/identity"

password=$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')
$engine run -d --name "$name" -e POSTGRES_PASSWORD="$password" -v "$work:/backup:ro" pgvector/pgvector:pg18 >/dev/null

for _ in $(seq 1 60); do
    if $engine exec "$name" pg_isready -q -U postgres -h 127.0.0.1; then
        break
    fi
    sleep 1
done

sql() { $engine exec -e PGPASSWORD="$password" "$name" psql -h 127.0.0.1 -U postgres -d restored -v ON_ERROR_STOP=1 -Atc "$1"; }

$engine exec -e PGPASSWORD="$password" "$name" createdb -h 127.0.0.1 -U postgres restored
$engine exec -e PGPASSWORD="$password" "$name" pg_restore -h 127.0.0.1 -U postgres -d restored --exit-on-error --no-owner --no-privileges /backup/db.dump

tables=$(sql "select count(*) from information_schema.tables where table_type = 'BASE TABLE' and table_schema not in ('pg_catalog', 'information_schema')")
history_table=$(sql "select quote_ident(table_schema) || '.' || quote_ident(table_name) from information_schema.tables where table_name = '__EFMigrationsHistory' limit 1")
users_table=$(sql "select quote_ident(table_schema) || '.' || quote_ident(table_name) from information_schema.tables where table_name = 'Users' limit 1")

if [ -z "$history_table" ] || [ -z "$users_table" ]; then
    echo "::error::The restored database has no migrations history or no Users table"
    exit 1
fi

migration=$(sql "select max(\"MigrationId\") from $history_table")
users=$(sql "select count(*) from $users_table")
echo "Restored: $tables tables, $users users, last migration $migration"

if [ "$tables" -lt "$min_tables" ] || [ "$users" -lt 1 ] || [ -z "$migration" ]; then
    echo "::error::The restored database is missing what a real one has"
    exit 1
fi

echo "The backup restores"
