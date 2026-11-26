#!/usr/bin/env bash
# Moves the database to a new major version of PostgreSQL (bitfabric-internal/specs/build-and-ship.md). The Docker Compose
# project runs this as its one-shot postgresserver-upgrade service, in the image postgresserver is about to run, and
# postgresserver starts only once it has exited with 0 (DockerComposePublishingExtensions). So every deployment of the
# project runs it, and Test and Production upgrade inside their deploy job with nothing done by hand on the server.
#
# A new major can't open an older major's data folder, so the new one gets a volume of its own, mounted at
# /var/lib/postgresql. While postgresserver still runs the older major, this copies every database into it with
# pg_dump and pg_restore and checks that each table holds as many rows. The older volume is never written to: it stays as
# it was for a rollback, and removing it is left to a person. The dumps stay in the postgresserver-upgrade volume.
#
# Safe to run twice: once the copy is done it leaves a marker in the new volume and does nothing from then on. A copy
# that stopped halfway is thrown away and made again, and a failure leaves the running server as it was.
set -Eeuo pipefail

data_root=/var/lib/postgresql
marker="$data_root/bitfabric-upgrade.txt"
new_data="$PGDATA"
new_major="$PG_MAJOR"
previous_data=/var/lib/postgresql-previous
dumps=/upgrade
host=postgresserver
user="${POSTGRES_USER:-postgres}"
export PGPASSWORD="$POSTGRES_PASSWORD"

say() { printf 'postgresserver-upgrade: %s\n' "$*"; }
now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
old() { psql -h "$host" -U "$user" -X -v ON_ERROR_STOP=1 -Atq "$@"; }
new() { psql -h 127.0.0.1 -U "$user" -X -v ON_ERROR_STOP=1 -Atq "$@"; }

if [[ -f "$marker" ]]; then
    say "nothing to do: $(head -n 1 "$marker")"
    exit 0
fi

# The server postgresserver runs now, if any: the last deployment's.
server_version=""
for _ in 1 2 3 4 5; do
    if server_version="$(PGCONNECT_TIMEOUT=5 old -d postgres -c 'show server_version_num' 2>/dev/null)"; then
        break
    fi
    server_version=""
    sleep 2
done

if [[ -z "$server_version" ]]; then
    if [[ -s "$new_data/PG_VERSION" ]]; then
        say "No server answers at $host, and $new_data holds a database whose copy never finished. Look at it before anything removes it."
        exit 1
    fi
    if [[ -s "$previous_data/PG_VERSION" ]]; then
        say "No server answers at $host to copy from, while the previous volume holds a PostgreSQL $(cat "$previous_data/PG_VERSION") database. Start the previous postgresserver again, then deploy again."
        exit 1
    fi
    printf '%s\n' "fresh: there was no earlier database, PostgreSQL $new_major starts empty ($(now))" > "$marker"
    say "$(head -n 1 "$marker")"
    exit 0
fi

server_major=$(( server_version / 10000 ))
if (( server_major >= new_major )); then
    printf '%s\n' "postgresserver already ran PostgreSQL $server_major when this first ran ($(now))" > "$marker"
    say "$(head -n 1 "$marker")"
    exit 0
fi

say "postgresserver runs PostgreSQL $server_major: copying its databases into PostgreSQL $new_major"

# Only the databases move: pg_dump can't carry a role's password, so a role other than the server's own user stops this.
other_roles="$(old -d postgres -c "select string_agg(rolname, ', ') from pg_roles where rolname !~ '^pg_' and rolname <> current_user")"
if [[ -n "$other_roles" ]]; then
    say "The server has roles besides $user ($other_roles), which this copy doesn't carry. Move them by hand first."
    exit 1
fi
mapfile -t databases < <(old -d postgres -c "select datname from pg_database where datallowconn and not datistemplate and datname <> 'postgres' order by 1")
if (( ${#databases[@]} == 0 )); then
    say "postgresserver answers but lists no database to copy, so nothing is moved. Look at it before deploying again."
    exit 1
fi

locked=()
server_pid=""
done=false
cleanup() {
    local status=$?
    if [[ -n "$server_pid" ]]; then
        kill -INT "$server_pid" 2>/dev/null || true
        wait "$server_pid" 2>/dev/null || true
    fi
    if [[ "$done" != true ]]; then
        for database in "${locked[@]}"; do
            printf 'alter database :"database" reset default_transaction_read_only;\n' | old -d postgres -v database="$database" -f - || true
        done
        rm -rf "$new_data"
        say "The upgrade failed, so postgresserver keeps running PostgreSQL $server_major on its own data, as it was."
        (( status != 0 )) || status=1
    fi
    exit "$status"
}
trap cleanup EXIT

if [[ -s "$new_data/PG_VERSION" ]]; then
    say "throwing away the unfinished copy of an earlier attempt"
    rm -rf "$new_data"
fi

# Nothing may write to the old databases once the copy starts, or it would miss what was written. They turn read-only and
# their connections close: the apps reconnect at once, can read, and get an error on a write, never a silent loss. They
# stay read-only afterwards, so a version that goes back to them can't write there by mistake.
for database in "${databases[@]}"; do
    printf 'alter database :"database" set default_transaction_read_only = on;\n' | old -d postgres -v database="$database" -f -
    locked+=("$database")
    printf 'select count(pg_terminate_backend(pid)) from pg_stat_activity where datname = :'"'"'database'"'"' and pid <> pg_backend_pid();\n' \
        | old -d postgres -v database="$database" -f - > /dev/null
done

# A fresh backup, taken before anything changes, and the copy's source.
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
folder="$dumps/$stamp-pg$server_major"
mkdir -p "$folder"
chmod 700 "$folder"
pg_dumpall -h "$host" -U "$user" --globals-only --no-role-passwords > "$folder/globals.sql"
for database in "${databases[@]}"; do
    pg_dump -h "$host" -U "$user" -Fc -f "$folder/$database.dump" "$database"
    say "dumped $database ($(du -h "$folder/$database.dump" | cut -f1))"
done

# The image's own entrypoint creates the cluster from POSTGRES_*, as postgresserver itself would have, and serves it
# here alone.
docker-entrypoint.sh postgres -c listen_addresses=127.0.0.1 &
server_pid=$!
for _ in $(seq 1 120); do
    if pg_isready -q -h 127.0.0.1 -U "$user" && new -d postgres -c 'select 1' > /dev/null 2>&1; then
        break
    fi
    sleep 1
done
new -d postgres -c 'select 1' > /dev/null

# Each table's rows, read on both sides.
counts() {
    printf '%s \\gexec\n' "select format('select %L, count(*) from %I.%I', n.nspname || '.' || c.relname, n.nspname, c.relname) from pg_class c join pg_namespace n on n.oid = c.relnamespace where c.relkind in ('r', 'p') and n.nspname not in ('pg_catalog', 'information_schema') and n.nspname !~ '^pg_toast' order by 1" \
        | "$@" -f -
}

tables=0
rows=0
for database in "${databases[@]}"; do
    printf 'create database :"database";\n' | new -d postgres -v database="$database" -f -
    pg_restore -h 127.0.0.1 -U "$user" -d "$database" --exit-on-error -j 4 "$folder/$database.dump"

    counts old -d "$database" > "$folder/$database.rows-before.txt"
    counts new -d "$database" > "$folder/$database.rows-after.txt"
    if ! diff "$folder/$database.rows-before.txt" "$folder/$database.rows-after.txt" > "$folder/$database.rows-diff.txt"; then
        say "$database's copy doesn't hold the same rows:"
        cat "$folder/$database.rows-diff.txt"
        exit 1
    fi
    if [[ "$(old -d "$database" -c 'select extname from pg_extension order by 1')" != "$(new -d "$database" -c 'select extname from pg_extension order by 1')" ]]; then
        say "$database's copy doesn't have the same extensions"
        exit 1
    fi
    tables=$(( tables + $(wc -l < "$folder/$database.rows-after.txt") ))
    rows=$(( rows + $(awk -F '|' '{ sum += $2 } END { print sum + 0 }' "$folder/$database.rows-after.txt") ))
    say "copied $database"
done

new -d postgres -c 'checkpoint' > /dev/null
kill -INT "$server_pid"
wait "$server_pid" || true
server_pid=""

printf '%s\n' \
    "upgraded from PostgreSQL $server_major to $new_major on $(now): ${#databases[@]} database(s), $tables tables, $rows rows, all matching" \
    "The PostgreSQL $server_major data stays in the volume mounted at $previous_data, untouched but for its databases now being read-only." \
    "To go back to it: ALTER DATABASE <name> RESET default_transaction_read_only; on each of: ${databases[*]}." \
    "Dumps: $folder in the postgresserver-upgrade volume." > "$marker"
done=true
say "$(head -n 1 "$marker")"
