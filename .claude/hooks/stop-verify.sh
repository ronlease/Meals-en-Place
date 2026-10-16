#!/usr/bin/env bash
# Stop: when .cs files changed, run a fast build and send failures back to Claude.
# Honors stop_hook_active so a failed build cannot trap Claude in a loop.
# Stays under 30 seconds: the build runs under a 25 second timeout and a timeout
# is not treated as a failure.

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
read_input

[[ "$(json_get stop_hook_active)" == "true" ]] && exit 0

changed="$(git -C "$PROJECT_DIR" status --porcelain -uall 2>/dev/null | grep -E '\.cs$' || true)"
[[ -z "$changed" ]] && exit 0

command -v dotnet >/dev/null 2>&1 || exit 0

# New migrations must acknowledge destructive changes.
if ! bash "$(dirname "${BASH_SOURCE[0]}")/guard-destructive-migration.sh" scan </dev/null; then
  exit 2
fi

output="$(cd "$PROJECT_DIR" && timeout 25 dotnet build MealsEnPlace.slnx --no-restore -nologo -v q -clp:ErrorsOnly 2>&1)"
code=$?

if [[ $code -eq 124 ]]; then
  echo "stop-verify: build exceeded 25s, skipped. Run dotnet build before committing." >&2
  exit 0
fi
if [[ $code -ne 0 ]]; then
  echo "BUILD FAILED. Fix these errors before finishing:" >&2
  echo "$output" | tail -30 >&2
  exit 2
fi
exit 0
