#!/bin/bash
# build-check.sh - Stop hook: auto build check after Claude finishes
# Exit 0 = build ok, Exit 2 = build failed (Claude continues to fix)

cd "$(dirname "$0")/../../server" || exit 0

output=$(dotnet build --nologo --verbosity quiet 2>&1)
exit_code=$?

if [ $exit_code -ne 0 ]; then
  echo "BUILD FAILED - please fix the following compilation errors:" >&2
  echo "$output" >&2
  exit 2
fi

exit 0
