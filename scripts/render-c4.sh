#!/usr/bin/env bash
#
# Renders the PlantUML C4 sources in docs/c4/ to PNG.
#
# The .puml files are the source of truth; the .png files are generated
# artifacts committed alongside them so the root README can embed them.
# Run this whenever a .puml changes and commit the regenerated PNGs — it is
# step 3 of the pre-PR checklist.
#
#   ./scripts/render-c4.sh          render, overwriting the committed PNGs
#   ./scripts/render-c4.sh --check  render to a temp dir and diff; no writes
#
# Rendering runs in Docker so the toolchain is pinned and reproducible without
# a local Java or Graphviz install. Docker Desktop must be running — the same
# prerequisite as the Postgres container.
#
# The diagrams !include C4-PlantUML from GitHub, so the render needs network
# access and the INTERNET security profile.

set -euo pipefail

# Pinned deliberately: PlantUML embeds no timestamps, but different versions
# lay out diagrams differently, so an unpinned image would churn every PNG.
readonly PLANTUML_IMAGE="plantuml/plantuml:1.2024.8"

readonly REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly C4_DIR="${REPO_ROOT}/docs/c4"

check_mode=false
if [[ "${1:-}" == "--check" ]]; then
  check_mode=true
elif [[ $# -gt 0 ]]; then
  echo "usage: $(basename "$0") [--check]" >&2
  exit 2
fi

if ! docker info >/dev/null 2>&1; then
  echo "ERROR: Docker is not available. Start Docker Desktop and retry." >&2
  exit 1
fi

# Render into a scratch directory first so a failed run cannot leave the
# committed PNGs half-written. The directory lives under the repo so Docker
# Desktop on Windows can bind-mount it without extra drive sharing.
work_dir="$(mktemp -d "${REPO_ROOT}/.c4-render.XXXXXX")"
trap 'rm -rf "${work_dir}"' EXIT
cp "${C4_DIR}"/*.puml "${work_dir}/"

# Git Bash / MSYS rewrites container-side paths such as /data into Windows
# paths, and Docker needs a Windows-style host path for the bind mount.
# cygpath exists only on those shells; elsewhere the POSIX path is already fine.
mount_src="${work_dir}"
if command -v cygpath >/dev/null 2>&1; then
  mount_src="$(cygpath -w "${work_dir}")"
fi

echo "Rendering $(ls -1 "${work_dir}"/*.puml | wc -l) diagram(s) with ${PLANTUML_IMAGE}..."
MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*' docker run --rm \
  -v "${mount_src}:/data" \
  -e PLANTUML_SECURITY_PROFILE=INTERNET \
  -w /data \
  "${PLANTUML_IMAGE}" \
  -tpng -failfast2 -nometadata '*.puml'

shopt -s nullglob
rendered=("${work_dir}"/*.png)
if [[ ${#rendered[@]} -eq 0 ]]; then
  echo "ERROR: PlantUML produced no PNGs." >&2
  exit 1
fi

if [[ "${check_mode}" == true ]]; then
  stale=()
  for png in "${rendered[@]}"; do
    name="$(basename "${png}")"
    if ! cmp -s "${png}" "${C4_DIR}/${name}"; then
      stale+=("${name}")
    fi
  done

  if [[ ${#stale[@]} -gt 0 ]]; then
    echo "ERROR: these committed PNGs do not match their .puml sources:" >&2
    printf '  %s\n' "${stale[@]}" >&2
    echo "Run ./scripts/render-c4.sh and commit the result." >&2
    exit 1
  fi

  echo "All C4 PNGs are current."
  exit 0
fi

for png in "${rendered[@]}"; do
  name="$(basename "${png}")"
  if cmp -s "${png}" "${C4_DIR}/${name}"; then
    echo "  unchanged  ${name}"
  else
    cp "${png}" "${C4_DIR}/${name}"
    echo "  updated    ${name}"
  fi
done

echo "Done. Commit any updated PNGs alongside the .puml change."
