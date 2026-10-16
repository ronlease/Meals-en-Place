#!/usr/bin/env bash
#
# One-time split of the monolithic docs/backlog.md into per-item files.
#
#   ./scripts/split-backlog.sh <source.md>            split into docs/backlog/
#   ./scripts/split-backlog.sh --verify <source.md>   rebuild the source from the
#                                                     split files and diff it
#
# Layout written under docs/backlog/:
#   header.md          text above the first item (kept so the split is lossless)
#   items/MEP-###.md   open items, verbatim
#   archive/MEP-###.md items whose status starts with "Done", verbatim
#   index.md           generated table: id, title, status, priority
#
# The verify mode concatenates header.md and every item in original order and
# must produce a byte-identical copy of the source.

set -euo pipefail

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly OUT_DIR="${REPO_ROOT}/docs/backlog"

verify=false
if [[ "${1:-}" == "--verify" ]]; then
  verify=true
  shift
fi

source_file="${1:?usage: split-backlog.sh [--verify] <source.md>}"

# Item boundaries: a line that starts with "## [MEP-###]".
ids=()
while IFS= read -r id; do
  ids+=("$id")
done < <(grep -oE '^## \[MEP-[0-9]+\]' "$source_file" | grep -oE 'MEP-[0-9]+')

if $verify; then
  rebuilt="$(mktemp)"
  trap 'rm -f "$rebuilt"' EXIT
  cat "${OUT_DIR}/header.md" > "$rebuilt"
  for id in "${ids[@]}"; do
    if [[ -f "${OUT_DIR}/items/${id}.md" ]]; then
      cat "${OUT_DIR}/items/${id}.md" >> "$rebuilt"
    else
      cat "${OUT_DIR}/archive/${id}.md" >> "$rebuilt"
    fi
  done
  if cmp -s "$source_file" "$rebuilt"; then
    echo "OK: ${#ids[@]} items, rebuilt file is byte-identical to the source"
  else
    echo "FAIL: rebuilt file differs from the source" >&2
    diff "$source_file" "$rebuilt" | head -20 >&2
    exit 1
  fi
  exit 0
fi

mkdir -p "${OUT_DIR}/items" "${OUT_DIR}/archive"

# Split with awk. Each item chunk runs from its heading to the line before the
# next heading. Status decides the destination directory.
awk -v out="$OUT_DIR" '
  function flush(   dir) {
    if (id == "") return
    dir = (status ~ /^Done/) ? "archive" : "items"
    printf "%s", buf > (out "/" dir "/" id ".md")
    close(out "/" dir "/" id ".md")
  }
  /^## \[MEP-[0-9]+\]/ {
    flush()
    match($0, /MEP-[0-9]+/)
    id = substr($0, RSTART, RLENGTH)
    status = ""
    buf = ""
    started = 1
  }
  !started { printf "%s\n", $0 > (out "/header.md"); next }
  /^\*\*Status:\*\*/ && status == "" {
    status = $0
    sub(/^\*\*Status:\*\* */, "", status)
  }
  { buf = buf $0 "\n" }
  END { flush() }
' "$source_file"

# Index.
{
  echo "# Backlog index"
  echo
  echo "One row per item. Open items live in \`items/\`, Done items in \`archive/\`."
  echo "Read an item file only when working it."
  echo
  echo "| ID | Title | Status | Priority |"
  echo "|---|---|---|---|"
  for id in "${ids[@]}"; do
    if [[ -f "${OUT_DIR}/items/${id}.md" ]]; then
      file="items/${id}.md"
    else
      file="archive/${id}.md"
    fi
    title="$(grep -m1 -E '^## \[' "${OUT_DIR}/${file}" | sed -E 's/^## \[MEP-[0-9]+\] *//')"
    status="$(grep -m1 -E '^\*\*Status:\*\*' "${OUT_DIR}/${file}" | sed -E 's/^\*\*Status:\*\* *//')"
    priority="$(grep -m1 -E '^\*\*Priority:\*\*' "${OUT_DIR}/${file}" | sed -E 's/^\*\*Priority:\*\* *//')"
    echo "| [${id}](${file}) | ${title//|/\\|} | ${status//|/\\|} | ${priority//|/\\|} |"
  done
} > "${OUT_DIR}/index.md"

echo "Split ${#ids[@]} items into ${OUT_DIR}"
