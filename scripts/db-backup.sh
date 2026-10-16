#!/usr/bin/env bash
#
# Dumps the local Postgres database with pg_dump, through docker compose.
#
#   ./scripts/db-backup.sh [output-file]
#
# Writes a custom-format dump (restore with pg_restore) to backups/ by default,
# named with a UTC timestamp. backups/ is gitignored. Nothing runs this script
# automatically: run it by hand before a risky migration or a bulk ingest.
#
# Restore:
#   docker compose exec -T db pg_restore -U mealsenplace -d mealsenplace --clean --if-exists < backups/<file>.dump

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly DB_SERVICE="db"
readonly DB_USER="mealsenplace"
readonly DB_NAME="mealsenplace"

output="${1:-${REPO_ROOT}/backups/mealsenplace-$(date -u +%Y%m%dT%H%M%SZ).dump}"
mkdir -p "$(dirname "$output")"

cd "$REPO_ROOT"

if ! docker compose ps --status running --services 2>/dev/null | grep -qx "$DB_SERVICE"; then
  echo "The '$DB_SERVICE' service is not running. Start it with: docker compose up -d" >&2
  exit 1
fi

# -Fc: compressed custom format. -T: no TTY, so the binary stream is not mangled.
docker compose exec -T "$DB_SERVICE" pg_dump -U "$DB_USER" -d "$DB_NAME" -Fc > "$output"

echo "Backup written to $output ($(du -h "$output" | cut -f1))"
