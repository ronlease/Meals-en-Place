#!/usr/bin/env bash
# Feeds simulated hook JSON to each hook and checks the exit code.
#   bash .claude/hooks/test-hooks.sh
# Uses throwaway git repos, so it never touches this repo's branch or files.

set -u
HOOKS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pass=0
fail=0

make_repo() { # $1 = branch
  local dir
  dir="$(mktemp -d)"
  git -C "$dir" init -q -b "$1"
  git -C "$dir" -c user.email=t@t -c user.name=t commit -q --allow-empty -m init
  echo "$dir"
}

expect() { # $1 = label, $2 = expected exit code, $3 = hook, $4 = project dir, $5 = json, rest = hook args
  local label="$1" want="$2" hook="$3" dir="$4" json="$5"
  shift 5
  local got
  CLAUDE_PROJECT_DIR="$dir" bash "$HOOKS/$hook" "$@" <<<"$json" >/dev/null 2>&1
  got=$?
  if [[ "$got" == "$want" ]]; then
    pass=$((pass + 1)); echo "PASS  $label (exit $got)"
  else
    fail=$((fail + 1)); echo "FAIL  $label (wanted $want, got $got)"
  fi
}

bash_json() { printf '{"tool_name":"Bash","tool_input":{"command":%s}}' "$(printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g; s/^/"/; s/$/"/')"; }

main_repo="$(make_repo main)"
feat_repo="$(make_repo chore/test)"

echo "== guard-main =="
expect "commit on main"                 2 guard-main.sh "$main_repo" "$(bash_json 'git commit -m x')"
expect "commit on feature branch"       0 guard-main.sh "$feat_repo" "$(bash_json 'git commit -m x')"
expect "checkout -b then commit"        0 guard-main.sh "$main_repo" "$(bash_json 'git checkout -b feat && git commit -m x')"
expect "add && commit on main"          2 guard-main.sh "$main_repo" "$(bash_json 'git add . && git commit -m x')"
expect "push origin main"               2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin main')"
expect "push HEAD:main"                 2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin HEAD:main')"
expect "bare push on main"              2 guard-main.sh "$main_repo" "$(bash_json 'git push')"
expect "push feature branch"            0 guard-main.sh "$feat_repo" "$(bash_json 'git push -u origin chore/test')"
expect "force push --force"             2 guard-main.sh "$feat_repo" "$(bash_json 'git push --force origin chore/test')"
expect "force push -f"                  2 guard-main.sh "$feat_repo" "$(bash_json 'git push -f origin chore/test')"
expect "force push with lease"          2 guard-main.sh "$feat_repo" "$(bash_json 'git push --force-with-lease')"
expect "force push +refspec"            2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin +chore/test')"
expect "push --all"                     2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin --all')"
expect "push --delete"                  2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin --delete chore/test')"
expect "push with variable expansion"   2 guard-main.sh "$feat_repo" "$(bash_json 'b=main; git push origin $b')"
expect "push with brace expansion"      2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin {main,x}')"
expect "push with escaped branch name"  2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin ma\in')"
expect "push with glob"                 2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin ma?n')"
expect "rm -rf is not a force-push"     0 guard-main.sh "$feat_repo" "$(bash_json 'rm -rf obj && git push origin chore/test')"
expect "quoted push to main"            2 guard-main.sh "$feat_repo" "$(bash_json 'git push origin "main"')"
expect "git -C push to main"            2 guard-main.sh "$feat_repo" "$(bash_json 'git -C . push origin main')"
expect "commit then checkout -b"        2 guard-main.sh "$main_repo" "$(bash_json 'git commit -m x && git checkout -b feat')"
expect "switch back to main then commit" 2 guard-main.sh "$feat_repo" "$(bash_json 'git switch main && git commit -m x')"
expect "git -C commit on main"          2 guard-main.sh "$main_repo" "$(bash_json 'git -C . commit -m x')"
expect "non-git command"                0 guard-main.sh "$main_repo" "$(bash_json 'ls -la')"
expect "git status on main"             0 guard-main.sh "$main_repo" "$(bash_json 'git status')"

echo "== guard-paths =="
for p in \
  "src/MealsEnPlace.Web/node_modules/x/index.js" \
  "src/MealsEnPlace.Api/bin/Debug/a.dll" \
  "src/MealsEnPlace.Api/obj/project.assets.json" \
  "src/MealsEnPlace.Web/coverage/index.html" \
  "coverage-report/Summary.txt" \
  "src/MealsEnPlace.Web/package-lock.json" \
  "src/MealsEnPlace.Api/Migrations/20260101_X.Designer.cs" \
  "docs/c4/context.png" \
  "src/MealsEnPlace.Tools.Ingest/data/recipes_data.csv"; do
  expect "blocks $p" 2 guard-paths.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$feat_repo/$p\"}}"
done
expect "allows API source"     0 guard-paths.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$feat_repo/src/MealsEnPlace.Api/Program.cs\"}}"
expect "allows migration .cs"  0 guard-paths.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$feat_repo/src/MealsEnPlace.Api/Migrations/20260101_X.cs\"}}"
expect "allows c4 puml"        0 guard-paths.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$feat_repo/docs/c4/context.puml\"}}"

echo "== guard-destructive-migration =="
mig="$feat_repo/src/MealsEnPlace.Api/Migrations/20260101_Drop.cs"
expect "Write DropTable no ack"  2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"migrationBuilder.DropTable(name: \\\"A\\\");\"}}"
expect "Write DropColumn no ack" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"migrationBuilder.DropColumn(name: \\\"A\\\");\"}}"
expect "Write DropTable with ack" 0 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"// DESTRUCTIVE-ACK: table unused since MEP-033\\nmigrationBuilder.DropTable(name: \\\"A\\\");\"}}"
expect "Write DropTable empty reason" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"// DESTRUCTIVE-ACK:\\nmigrationBuilder.DropTable(name: \\\"A\\\");\"}}"
expect "Write raw SQL DROP TABLE" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"migrationBuilder.Sql(\\\"drop table x\\\");\"}}"
expect "Write raw SQL TRUNCATE" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"migrationBuilder.Sql(\\\"TRUNCATE x\\\");\"}}"
expect "Write DropSchema" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"migrationBuilder.DropSchema(name: \\\"s\\\");\"}}"
expect "Write ack inside a string is not an ack" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"var s = \\\"// DESTRUCTIVE-ACK: x\\\"; migrationBuilder.DropTable(name: \\\"A\\\");\"}}"
expect "Write AddColumn"        0 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$mig\",\"content\":\"migrationBuilder.AddColumn<int>(name: \\\"A\\\");\"}}"
expect "Write non-migration"     0 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$feat_repo/src/X.cs\",\"content\":\"DropTable\"}}"
expect "Write Designer skipped"  0 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$feat_repo/src/MealsEnPlace.Api/Migrations/20260101_Drop.Designer.cs\",\"content\":\"DropTable\"}}"
mkdir -p "$feat_repo/src/MealsEnPlace.Api/Migrations"
printf 'migrationBuilder.DropColumn(name: "A");\n' > "$mig"
expect "scan new file no ack"    2 guard-destructive-migration.sh "$feat_repo" "$(bash_json 'dotnet ef migrations add Drop')" scan
printf '// DESTRUCTIVE-ACK: reason\nmigrationBuilder.DropColumn(name: "A");\n' > "$mig"
expect "scan new file with ack"  0 guard-destructive-migration.sh "$feat_repo" "$(bash_json 'dotnet ef migrations add Drop')" scan
printf 'migrationBuilder.DropColumn(name: "A");\n' > "$mig"
expect "scan ignores other cmds" 0 guard-destructive-migration.sh "$feat_repo" "$(bash_json 'ls')" scan
printf 'migrationBuilder.AddColumn<int>(name: "A");\n' > "$mig"
expect "Edit introduces DropColumn" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$mig\",\"old_string\":\"AddColumn<int>\",\"new_string\":\"DropColumn\"}}"
printf '// DESTRUCTIVE-ACK: reason\nmigrationBuilder.AddColumn<int>(name: "A");\n' > "$mig"
expect "Edit DropColumn with ack elsewhere" 0 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$mig\",\"old_string\":\"AddColumn<int>\",\"new_string\":\"DropColumn\"}}"
printf 'migrationBuilder.DropColumn(name: "A");\n' > "$mig"
expect "Edit removing the drop" 0 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$mig\",\"old_string\":\"DropColumn\",\"new_string\":\"AddColumn<int>\"}}"
expect "Edit that does not apply fails closed" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"$mig\",\"old_string\":\"NotInFile\",\"new_string\":\"migrationBuilder.DropTable(name: 1);\"}}"
expect "Edit with odd path casing" 2 guard-destructive-migration.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$feat_repo/src/MealsEnPlace.Api/MIGRATIONS/20260101_Drop.CS\",\"content\":\"DropTable\"}}"
rm -f "$mig"

echo "== session-start =="
if [[ "$(uname -s)" == "Linux" ]]; then
  expect "linux with tools" 0 session-start.sh "$feat_repo" "{}"
else
  expect "non-linux is rejected" 2 session-start.sh "$feat_repo" "{}"
fi
fake_bin="$(mktemp -d)"
printf '#!/usr/bin/env bash\necho Linux\n' > "$fake_bin/uname"; chmod +x "$fake_bin/uname"
got=$(PATH="$fake_bin:/usr/bin:/bin" bash "$HOOKS/session-start.sh" 2>&1 >/dev/null; echo "exit=$?")
if grep -q "dotnet is not on PATH" <<<"$got" && grep -q "exit=2" <<<"$got"; then
  pass=$((pass + 1)); echo "PASS  linux without dotnet/node is rejected with a message"
else
  fail=$((fail + 1)); echo "FAIL  linux without dotnet/node: $got"
fi

echo "== stop-verify =="
expect "stop_hook_active skips"  0 stop-verify.sh "$feat_repo" '{"stop_hook_active":true}'
expect "no .cs changes skips"    0 stop-verify.sh "$feat_repo" '{"stop_hook_active":false}'
fake_dotnet="$(mktemp -d)"
printf '#!/usr/bin/env bash
echo "Program.cs(1,1): error CS1002: ; expected"
exit 1
' > "$fake_dotnet/dotnet"; chmod +x "$fake_dotnet/dotnet"
echo "class A {}" > "$feat_repo/A.cs"
got=$(PATH="$fake_dotnet:$PATH" CLAUDE_PROJECT_DIR="$feat_repo" bash "$HOOKS/stop-verify.sh" <<<'{"stop_hook_active":false}' 2>&1 >/dev/null; echo "exit=$?")
if grep -q "BUILD FAILED" <<<"$got" && grep -q "exit=2" <<<"$got"; then pass=$((pass + 1)); echo "PASS  changed .cs + failing build exits 2 with errors"; else fail=$((fail + 1)); echo "FAIL  failing build: $got"; fi
got=$(PATH="$fake_dotnet:$PATH" CLAUDE_PROJECT_DIR="$feat_repo" bash "$HOOKS/stop-verify.sh" <<<'{"stop_hook_active":true}' 2>&1 >/dev/null; echo "exit=$?")
if grep -q "exit=0" <<<"$got"; then pass=$((pass + 1)); echo "PASS  stop_hook_active=true skips a failing build"; else fail=$((fail + 1)); echo "FAIL  loop guard: $got"; fi
printf '#!/usr/bin/env bash
exit 0
' > "$fake_dotnet/dotnet"
PATH="$fake_dotnet:$PATH" expect "changed .cs + passing build" 0 stop-verify.sh "$feat_repo" '{"stop_hook_active":false}'
rm -f "$feat_repo/A.cs"; rm -rf "$fake_dotnet"

echo "== format-on-edit =="
expect "missing file is ignored" 0 format-on-edit.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$feat_repo/none.cs\"}}"
expect "unrelated file ignored"  0 format-on-edit.sh "$feat_repo" "{\"tool_name\":\"Write\",\"tool_input\":{\"file_path\":\"$feat_repo/README.md\"}}"

rm -rf "$main_repo" "$feat_repo" "$fake_bin"
echo
echo "passed=$pass failed=$fail"
[[ $fail -eq 0 ]]
