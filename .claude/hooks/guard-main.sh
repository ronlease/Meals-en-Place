#!/usr/bin/env bash
# PreToolUse (Bash): block commit and push on main, and block every force-push.
# Exit 2 blocks the call and sends stderr back to Claude.

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
read_input

command_line="$(json_get tool_input.command)"
[[ -z "$command_line" ]] && exit 0

branch="$(git -C "$PROJECT_DIR" branch --show-current 2>/dev/null || true)"
on_protected=false
if [[ "$branch" == "main" || "$branch" == "master" ]]; then
  on_protected=true
fi

# `git`, optionally followed by global options such as -C <dir> or -c key=value.
readonly GIT='git([[:space:]]+(-C|-c)[[:space:]]+[^[:space:]]+)*'
readonly END='([[:space:]]|$)'
readonly FORCE_FLAGS='(^|[[:space:]])(--force[a-z-]*|--mirror|-[a-zA-Z]*f[a-zA-Z]*)([[:space:]=]|$)'
readonly FORCE_REFSPEC='(^|[[:space:]])\+[^[:space:]]+'
readonly PROTECTED_REF='(^|[[:space:]:/])(main|master)([[:space:]]|$)'

# Walk the shell segments in order. A branch switch only counts for commands
# that come after it. Quotes are stripped so `git push origin "main"` reads as main.
# Splitting on ; & | keeps `rm -rf x && git commit` from looking like a force-push.
while IFS= read -r segment; do
  raw_segment="$segment"
  segment="$(tr -d "\"'\134" <<<"$segment" | sed -E 's/^[[:space:]]+//')"

  # Track the effective branch. Creating a branch leaves main. Switching to main or master returns to it.
  # Other checkouts (file restores, unknown refs) leave the state alone, which fails closed.
  if grep -Eq "^${GIT}[[:space:]]+(checkout[[:space:]]+-[bB]|switch[[:space:]]+(-c|-C|--create))${END}" <<<"$segment"; then
    on_protected=false
  elif grep -Eq "^${GIT}[[:space:]]+(checkout|switch)${END}" <<<"$segment" && grep -Eq "$PROTECTED_REF" <<<"$segment"; then
    on_protected=true
  fi

  if grep -Eq "^${GIT}[[:space:]]+commit${END}" <<<"$segment"; then
    if [[ "$on_protected" == true ]]; then
      block "commit on '$branch'. Create a feature branch first (git switch -c <name>)."
    fi
  fi

  if grep -Eq "^${GIT}[[:space:]]+push${END}" <<<"$segment"; then
    # Fail closed on shell expansion, which the checks below cannot see through.
    if grep -Eq '[$`{}*?]|\\' <<<"$raw_segment"; then
      block "git push with shell expansion or escapes ($, backtick, braces, *, ?, backslash) is not allowed. Spell out the branch."
    fi
    if grep -Eq "$FORCE_FLAGS" <<<"$segment" || grep -Eq "$FORCE_REFSPEC" <<<"$segment"; then
      block "force-push is not allowed."
    fi
    if grep -Eq '(^|[[:space:]])--(all|mirror|delete)([[:space:]=]|$)' <<<"$segment"; then
      block "push --all, --mirror and --delete are not allowed."
    fi
    if grep -Eq "$PROTECTED_REF" <<<"$segment"; then
      block "push to main. Push a feature branch instead."
    fi
    if [[ "$on_protected" == true ]]; then
      block "push from '$branch'. Switch to a feature branch."
    fi
  fi
done < <(tr ';&|' '\n\n\n' <<<"$command_line")

exit 0
