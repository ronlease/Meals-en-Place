#!/usr/bin/env bash
# SessionStart: confirm the environment this repo expects.
# Linux only (WSL on Windows, native on Fedora), with dotnet and node on PATH.

problems=()

kernel="$(uname -s 2>/dev/null || echo unknown)"
if [[ "$kernel" != "Linux" ]]; then
  problems+=("Not running on Linux (uname reports '$kernel'). Open this repo in WSL on Windows or run it on Fedora.")
fi
command -v dotnet >/dev/null 2>&1 || problems+=("dotnet is not on PATH. Install the .NET 10 SDK.")
command -v node >/dev/null 2>&1 || problems+=("node is not on PATH. Install Node.js 22 or newer.")

if [[ ${#problems[@]} -gt 0 ]]; then
  echo "Environment check failed:" >&2
  for p in "${problems[@]}"; do
    echo "  - $p" >&2
  done
  exit 2
fi
exit 0
