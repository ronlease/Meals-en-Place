#!/usr/bin/env bash
# Block migration files that drop a table or column without an acknowledgement.
#
# A migration containing DropTable or DropColumn must also contain a line:
#   // DESTRUCTIVE-ACK: <reason>
#
# Two modes:
#   (no args)  PreToolUse on Edit|Write: check the content about to be written.
#   scan       PostToolUse on Bash (after `dotnet ef migrations add`) and Stop:
#              check migration files that git reports as new or modified.
# Exit 2 blocks (PreToolUse) or feeds the message back to Claude (other events).

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

is_migration_file() {
  case "$1" in
    */Migrations/*.Designer.cs | */Migrations/*ModelSnapshot.cs) return 1 ;;
    */Migrations/*.cs) return 0 ;;
    *) return 1 ;;
  esac
}

violates() {
  # stdin: file text
  local text
  text="$(cat)"
  grep -Eq 'DropTable|DropColumn' <<<"$text" || return 1
  grep -Eq '//[[:space:]]*DESTRUCTIVE-ACK:[[:space:]]*[^[:space:]]' <<<"$text" && return 1
  return 0
}

message() {
  echo "BLOCKED: $1 drops a table or column without acknowledgement." >&2
  echo "Add a line '// DESTRUCTIVE-ACK: <reason>' to the migration, and tell the user what data is lost." >&2
}

if [[ "${1:-}" == "scan" ]]; then
  read_input
  # PostToolUse on Bash: only react to migration scaffolding.
  command_line="$(json_get tool_input.command)"
  if [[ -n "$command_line" ]] && ! grep -Eq 'migrations[[:space:]]+add' <<<"$command_line"; then
    exit 0
  fi
  status=0
  while IFS= read -r line; do
    file="${line:3}"
    is_migration_file "$file" || continue
    [[ -f "$PROJECT_DIR/$file" ]] || continue
    if violates <"$PROJECT_DIR/$file"; then
      message "$file"
      status=2
    fi
  done < <(git -C "$PROJECT_DIR" status --porcelain -uall 2>/dev/null)
  exit $status
fi

read_input
file_path="$(json_get tool_input.file_path)"
[[ -z "$file_path" ]] && exit 0
rel="$(normalize_path "$file_path")"
is_migration_file "$rel" || exit 0

tool_name="$(json_get tool_name)"
if [[ "$tool_name" == "Write" ]]; then
  new_text="$(json_get tool_input.content)"
else
  # Edit: judge the file as it will look after the edit (existing text plus the replacement).
  existing=""
  [[ -f "$PROJECT_DIR/$rel" ]] && existing="$(cat "$PROJECT_DIR/$rel")"
  new_text="${existing}"$'\n'"$(json_get tool_input.new_string)"
fi

if violates <<<"$new_text"; then
  message "$rel"
  exit 2
fi
exit 0
