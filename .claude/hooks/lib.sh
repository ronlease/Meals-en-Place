#!/usr/bin/env bash
# Shared helpers for Claude Code hooks. Source this file; do not run it.
#
# Hook input arrives as JSON on stdin. Call `read_input` once, then use
# `json_get <dotted.path>` to read a field. Uses jq when present and falls back
# to Python (python3, then python) so the hooks work on a bare Fedora or WSL box.

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
INPUT=""

read_input() {
  INPUT="$(cat)"
}

json_get() {
  local path="$1"
  if command -v jq >/dev/null 2>&1; then
    jq -r ".${path} // empty" <<<"$INPUT"
    return
  fi
  local py
  for py in python3 python; do
    if "$py" -c '' >/dev/null 2>&1; then
      "$py" -c '
import json, sys
try:
    data = json.loads(sys.stdin.read())
except ValueError:
    sys.exit(0)
for key in sys.argv[1].split("."):
    data = data.get(key) if isinstance(data, dict) else None
if data is None:
    sys.exit(0)
print(data if isinstance(data, str) else json.dumps(data))
' "$path" <<<"$INPUT"
      return
    fi
  done
  echo "hook error: need jq or python to parse hook input" >&2
  return 1
}

# Print the path with backslashes turned into slashes and the project prefix removed.
normalize_path() {
  local p="${1//\\//}"
  local root="${PROJECT_DIR//\\//}"
  echo "${p#"$root"/}"
}

block() {
  echo "BLOCKED: $1" >&2
  exit 2
}
