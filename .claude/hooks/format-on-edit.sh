#!/usr/bin/env bash
# PostToolUse (Edit|Write): format the file Claude just wrote.
#   .cs                     -> dotnet format on the owning project, limited to this file
#   .ts .html .scss in Web  -> prettier --write
# Formatting problems never block the edit: this hook always exits 0.

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
read_input

file_path="$(json_get tool_input.file_path)"
[[ -z "$file_path" ]] && exit 0
rel="$(normalize_path "$file_path")"
[[ -f "$PROJECT_DIR/$rel" ]] || exit 0

case "$rel" in
  *.cs)
    case "$rel" in
      */Migrations/*) exit 0 ;;
      src/MealsEnPlace.Api/*) project="src/MealsEnPlace.Api" ;;
      src/MealsEnPlace.Tools.Ingest/*) project="src/MealsEnPlace.Tools.Ingest" ;;
      tests/MealsEnPlace.Unit/*) project="tests/MealsEnPlace.Unit" ;;
      tests/MealsEnPlace.Integration/*) project="tests/MealsEnPlace.Integration" ;;
      *) exit 0 ;;
    esac
    command -v dotnet >/dev/null 2>&1 || exit 0
    (cd "$PROJECT_DIR" && dotnet format "$project" --no-restore --include "$rel" --verbosity quiet) >&2 || true
    ;;
  src/MealsEnPlace.Web/src/*.ts | src/MealsEnPlace.Web/src/*.html | src/MealsEnPlace.Web/src/*.scss)
    web_dir="$PROJECT_DIR/src/MealsEnPlace.Web"
    prettier="$web_dir/node_modules/.bin/prettier"
    [[ -x "$prettier" ]] || exit 0
    (cd "$web_dir" && "$prettier" --write "${rel#src/MealsEnPlace.Web/}") >&2 || true
    ;;
esac

exit 0
