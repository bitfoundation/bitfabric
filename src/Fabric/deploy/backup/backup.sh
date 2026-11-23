#!/usr/bin/env bash
# Backs up the Production database and files to the locked R2 bucket (bitfabric-internal/specs/build-and-ship.md).
# The server holds only the public half of the encryption key and an R2 key that cannot change the bucket's lock, so
# someone who takes over the server can neither read the older backups nor delete them.
set -euo pipefail

: "${BACKUP_AGE_RECIPIENT:?}" "${BACKUP_BUCKET:?}" "${BACKUP_FILES_BUCKET:?}"

project=bit-fabric-production
stamp=$(date -u +%Y-%m-%dT%H%MZ)
day=${stamp%T*}
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

container=$(docker ps -q --filter "label=com.docker.compose.project=$project" --filter label=com.docker.compose.service=postgresserver)
if [ -z "$container" ]; then
    echo "The database container isn't running" >&2
    exit 1
fi

# pg_dump reads one consistent snapshot of the running database. Encrypted on the way out: nothing plain touches the disk.
docker exec "$container" sh -c '
    export PGPASSWORD="$POSTGRES_PASSWORD"
    user="${POSTGRES_USER:-postgres}"
    database=$(psql -h 127.0.0.1 -U "$user" -d postgres -Atc "select datname from pg_database where not datistemplate and datname <> '"'"'postgres'"'"' limit 1")
    pg_dump -h 127.0.0.1 -U "$user" -Fc "$database"
' | age -r "$BACKUP_AGE_RECIPIENT" -o "$work/db.dump.age"

if [ ! -s "$work/db.dump.age" ]; then
    echo "The backup is empty" >&2
    exit 1
fi

rclone copyto "$work/db.dump.age" "backup:$BACKUP_BUCKET/db/$stamp.dump.age"

# One folder a day: a file replaced during the day lands in the next day's folder, as the locked ones can't be overwritten.
rclone copy --ignore-existing "backup:$BACKUP_FILES_BUCKET" "backup:$BACKUP_BUCKET/files/$day"

# Last, so that it means the whole run worked. The health report reads this (BackupHealthCheck).
echo "$stamp" > "$work/finished"
rclone copyto "$work/finished" "backup:$BACKUP_BUCKET/finished/$stamp"

echo "Backed up db/$stamp.dump.age and files/$day"
