#!/usr/bin/env bash
# Step 2 of moving the database to a new major version of PostgreSQL; export.sh is step 1
# (bitfabric-internal/specs/build-and-ship.md). The Docker Compose project runs this as its one-shot
# postgresserver-upgrade service, in the image postgresserver is about to run, after postgresserver-export, and
# postgresserver starts only once it has exited with 0 (DockerComposePublishingExtensions). So every deployment runs it,
# and Test and Production upgrade inside their deploy job with nothing done by hand on the server.
#
# A new major can't open an older major's data folder, so the new one has a volume of its own, mounted at
# /var/lib/postgresql. This restores the dumps postgresserver-export took into it, checks that each table holds the rows
# it held before and that the extensions match, and leaves a marker. From then on both steps do nothing. The older
# volume is never written to: it stays for a rollback, and removing it is left to a person, as are the dumps.
set -Eeuo pipefail

data_root=/var/lib/postgresql
marker="$data_root/bitfabric-upgrade.txt"
new_data="$PGDATA"
new_major="$PG_MAJOR"
previous=/var/lib/postgresql-previous
pending=/upgrade/pending
user="${POSTGRES_USER:-postgres}"
export PGPASSWORD="$POSTGRES_PASSWORD"

say() { printf 'postgresserver-upgrade: %s\n' "$*"; }
now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
sql() { psql -h 127.0.0.1 -U "$user" -X -v ON_ERROR_STOP=1 -Atq "$@"; }

# Written whole or not at all: a marker means the copy is done, and from then on nothing touches the new volume.
write_marker() {
    printf '%s\n' "$@" > "$marker.tmp"
    mv -f "$marker.tmp" "$marker"
}

if [[ -f "$marker" ]]; then
    say "nothing to do: $(head -n 1 "$marker")"
    exit 0
fi
rm -f "$marker.tmp"

if [[ ! -f "$pending" ]]; then
    if [[ -s "$previous/PG_VERSION" ]]; then
        say "The earlier volume holds a PostgreSQL $(cat "$previous/PG_VERSION") database, but postgresserver-export left no dump of it. PostgreSQL $new_major won't start empty next to it."
        exit 1
    fi
    if [[ -s "$new_data/PG_VERSION" ]]; then
        write_marker "kept: PostgreSQL $(cat "$new_data/PG_VERSION") data was already here, with no earlier database to copy ($(now))"
    else
        write_marker "fresh: there was no earlier database, PostgreSQL $new_major starts empty ($(now))"
    fi
    say "$(head -n 1 "$marker")"
    exit 0
fi
folder="$(cat "$pending")"
database_list="$(cat "$folder/databases.txt")"
if [[ -z "$database_list" ]]; then
    say "postgresserver-export's list of databases in $folder is empty, so nothing is moved. Look at it before deploying again."
    exit 1
fi
mapfile -t databases <<< "$database_list"

server_pid=""
done=false
cleanup() {
    local status=$?
    if [[ -n "$server_pid" ]]; then
        kill -INT "$server_pid" 2>/dev/null || true
        wait "$server_pid" 2>/dev/null || true
    fi
    if [[ "$done" != true && ! -f "$marker" ]]; then
        rm -f "$marker.tmp"
        rm -rf "$new_data"
        say "The upgrade failed and its copy is gone. The earlier volume is as it was, and the dumps stay in $folder."
        (( status != 0 )) || status=1
    fi
    exit "$status"
}
trap cleanup EXIT

# postgresserver waits for this service, so data here without the marker is an unfinished copy no server ever used.
if [[ -s "$new_data/PG_VERSION" ]]; then
    say "throwing away the unfinished copy of an earlier attempt"
    rm -rf "$new_data"
fi

# The image's own entrypoint creates the cluster from POSTGRES_*, as postgresserver itself would have, and serves it
# here alone.
docker-entrypoint.sh postgres -c listen_addresses=127.0.0.1 &
server_pid=$!
for _ in $(seq 1 120); do
    if pg_isready -q -h 127.0.0.1 -U "$user" && sql -d postgres -c 'select 1' > /dev/null 2>&1; then
        break
    fi
    sleep 1
done
sql -d postgres -c 'select 1' > /dev/null

counts() {
    printf '%s \\gexec\n' "select format('select %L, count(*) from %I.%I', n.nspname || '.' || c.relname, n.nspname, c.relname) from pg_class c join pg_namespace n on n.oid = c.relnamespace where c.relkind in ('r', 'p') and n.nspname not in ('pg_catalog', 'information_schema') and n.nspname !~ '^pg_toast' order by 1" \
        | sql -d "$1" -f -
}
settings() {
    printf '%s\n' "select coalesce(r.rolname, '') || '|' || array_to_string(s.setconfig, ',') from pg_db_role_setting s join pg_database d on d.oid = s.setdatabase left join pg_roles r on r.oid = s.setrole where d.datname = :'database' order by 1" \
        | sql -d postgres -v database="$1" -f -
}

tables=0
rows=0
for database in "${databases[@]}"; do
    # --create makes the database from the archive itself: its encoding and locale, and its own settings
    # (ALTER DATABASE ... SET), which a restore into a database made here would leave out.
    pg_restore -h 127.0.0.1 -U "$user" -d postgres --create --exit-on-error -j 4 "$folder/$database.dump"

    if [[ "$(cat "$folder/$database.settings.txt")" != "$(settings "$database")" ]]; then
        say "$database's copy doesn't have the same database settings"
        exit 1
    fi

    counts "$database" > "$folder/$database.rows-after.txt"
    if ! diff "$folder/$database.rows.txt" "$folder/$database.rows-after.txt" > "$folder/$database.rows-diff.txt"; then
        say "$database's copy doesn't hold the same rows:"
        cat "$folder/$database.rows-diff.txt"
        exit 1
    fi
    if [[ "$(cat "$folder/$database.extensions.txt")" != "$(sql -d "$database" -c 'select extname from pg_extension order by 1')" ]]; then
        say "$database's copy doesn't have the same extensions"
        exit 1
    fi
    tables=$(( tables + $(wc -l < "$folder/$database.rows-after.txt") ))
    rows=$(( rows + $(awk -F '|' '{ sum += $2 } END { print sum + 0 }' "$folder/$database.rows-after.txt") ))
    say "copied $database"
done

sql -d postgres -c 'checkpoint' > /dev/null
kill -INT "$server_pid"
wait "$server_pid" || true
server_pid=""

write_marker \
    "upgraded to PostgreSQL $new_major on $(now): ${#databases[@]} database(s), $tables tables, $rows rows, all matching" \
    "The earlier data stays as it was in the volume mounted at $previous, for a rollback." \
    "Dumps: $folder in the postgresserver-upgrade volume."
done=true
rm -f "$pending" || say "couldn't remove $pending, which the marker makes harmless"
say "$(head -n 1 "$marker")"
