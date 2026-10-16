#!/usr/bin/env bash
# PreToolUse (Bash): block commit and push on main, and block every force-push.
# Exit 2 blocks the call and sends stderr back to Claude.

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
read_input

command_line="$(json_get tool_input.command)"
[[ -z "$command_line" ]] && exit 0

branch="$(git -C "$PROJECT_DIR" branch --show-current 2>/dev/null || true)"
creates_branch=false
if grep -Eq 'git[[:space:]]+(checkout[[:space:]]+-[bB]|switch[[:space:]]+(-c|-C|--create))' <<<"$command_line"; then
  creates_branch=true
fi

# Check each shell segment on its own so `rm -rf x && git commit` is not a force-push.
while IFS= read -r segment; do
  segment="$(sed -E 's/^[[:space:]]+//' <<<"$segment")"

  if grep -Eq '^git([[:space:]]+-C[[:space:]]+[^[:space:]]+)?[[:space:]]+commit([[:space:]]|$)' <<<"$segment"; then
    if [[ ("$branch" == "main" || "$branch" == "master") && "$creates_branch" == false ]]; then
      block "commit on '$branch'. Create a feature branch first (git switch -c <name>)."
    fi
  fi

  if grep -Eq '^git([[:space:]]+-C[[:space:]]+[^[:space:]]+)?[[:space:]]+push([[:space:]]|$)' <<<"$segment"; then
    # Force flags: --force, --force-with-lease, --force-if-includes, -f, bundled short flags with f, +refspec.
    if grep -Eq '(^|[[:space:]])(--force[a-z-]*|--mirror|-[a-zA-Z]*f[a-zA-Z]*)([[:space:]=]|$)' <<<"$segment" \
      || grep -Eq '(^|[[:space:]])\+[^[:space:]]+' <<<"$segment"; then
      block "force-push is not allowed."
    fi
    # Explicit push to main or master, in any refspec form.
    if grep -Eq '(^|[[:space:]:/])(main|master)([[:space:]]|$)' <<<"$segment"; then
      block "push to main. Push a feature branch instead."
    fi
    # Bare push while on main.
    if [[ ("$branch" == "main" || "$branch" == "master") && "$creates_branch" == false ]]; then
      block "push from '$branch'. Switch to a feature branch."
    fi
  fi
done < <(tr ';&|' '\n\n\n' <<<"$command_line")

exit 0
