#!/usr/bin/env bash
# Step 1 of moving the database to a new major version of PostgreSQL; upgrade.sh is step 2
# (bitfabric-internal/specs/build-and-ship.md). The Docker Compose project runs this as its one-shot
# postgresserver-export service, in the image of the major the data was written with, before postgresserver-upgrade and
# postgresserver (DockerComposePublishingExtensions). By then Compose has stopped the postgresserver container that served
# that data, since it replaces a container before it starts what the new one waits for, so nothing writes to it.
#
# The older volume is mounted read-only and is never written to: this copies its data folder, runs that major on the
# copy alone, and dumps every database, with each table's row count and the extensions, into the postgresserver-upgrade
# volume, where upgrade.sh takes them from.
set -Eeuo pipefail

previous=/var/lib/postgresql-previous
new_marker=/var/lib/postgresql-new/bitfabric-upgrade.txt
dumps=/upgrade
scratch=/tmp/previous-data
user="${POSTGRES_USER:-postgres}"

say() { printf 'postgresserver-export: %s\n' "$*"; }

if [[ -f "$new_marker" ]]; then
    say "nothing to do: $(head -n 1 "$new_marker")"
    exit 0
fi

rm -f "$dumps/pending"
if [[ ! -s "$previous/PG_VERSION" ]]; then
    say "there is no earlier database to export"
    exit 0
fi
previous_major="$(cat "$previous/PG_VERSION")"
if [[ "$previous_major" != "$PG_MAJOR" ]]; then
    say "The earlier volume holds a PostgreSQL $previous_major database, which this service, PostgreSQL $PG_MAJOR, can't read."
    exit 1
fi

pg() { gosu postgres "$@"; }
sql() { psql -h /tmp -U "$user" -X -v ON_ERROR_STOP=1 -Atq "$@"; }
cleanup() {
    local status=$?
    pg pg_ctl -D "$scratch" -m fast -w stop > /dev/null 2>&1 || true
    rm -rf "$scratch"
    (( status == 0 )) || say "The export failed. The earlier volume is as it was."
    exit "$status"
}
trap cleanup EXIT

# A copy, so that running the server, which writes to its data folder, leaves the volume itself as it was. It is only
# reachable through its socket here, by trust, as nothing else can reach this container's socket.
say "copying the PostgreSQL $previous_major data folder to run it here alone"
rm -rf "$scratch"
mkdir -p "$scratch"
cp -a "$previous/." "$scratch/"
rm -f "$scratch/postmaster.pid"
printf 'local all all trust\n' > "$scratch/pg_hba.conf"
chown -R postgres:postgres "$scratch"
chmod 700 "$scratch"
pg pg_ctl -D "$scratch" -o "-c listen_addresses='' -c unix_socket_directories=/tmp" -l /tmp/previous-server.log -w -t 600 start \
    || { cat /tmp/previous-server.log; exit 1; }

# Only the databases move: pg_dump can't carry a role's password, so a role other than the server's own user stops this.
other_roles="$(sql -d postgres -c "select string_agg(rolname, ', ') from pg_roles where rolname !~ '^pg_' and rolname <> '$user'")"
if [[ -n "$other_roles" ]]; then
    say "The database has roles besides $user ($other_roles), which this copy doesn't carry. Move them by hand first."
    exit 1
fi
mapfile -t databases < <(sql -d postgres -c "select datname from pg_database where datallowconn and not datistemplate and datname <> 'postgres' order by 1")

# Each table's rows, for upgrade.sh to check its copy against.
counts() {
    printf '%s \\gexec\n' "select format('select %L, count(*) from %I.%I', n.nspname || '.' || c.relname, n.nspname, c.relname) from pg_class c join pg_namespace n on n.oid = c.relnamespace where c.relkind in ('r', 'p') and n.nspname not in ('pg_catalog', 'information_schema') and n.nspname !~ '^pg_toast' order by 1" \
        | sql -d "$1" -f -
}

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
folder="$dumps/$stamp-pg$previous_major"
mkdir -p "$folder"
chmod 700 "$folder"
pg_dumpall -h /tmp -U "$user" --globals-only --no-role-passwords > "$folder/globals.sql"
for database in "${databases[@]}"; do
    counts "$database" > "$folder/$database.rows.txt"
    sql -d "$database" -c 'select extname from pg_extension order by 1' > "$folder/$database.extensions.txt"
    pg_dump -h /tmp -U "$user" -Fc -f "$folder/$database.dump" "$database"
    say "dumped $database ($(du -h "$folder/$database.dump" | cut -f1), $(wc -l < "$folder/$database.rows.txt") tables)"
done
: > "$folder/databases.txt"
for database in "${databases[@]}"; do
    printf '%s\n' "$database" >> "$folder/databases.txt"
done
printf '%s\n' "$folder" > "$dumps/pending"
say "exported ${#databases[@]} database(s) from PostgreSQL $previous_major to $folder"
