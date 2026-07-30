#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
UNITY_PROJECT_ROOT="$REPO_ROOT/client"

cd "$UNITY_PROJECT_ROOT"

COMMAND="${1:-}"
HAS_PROJECT_PATH=0
for ARG in "$@"; do
    if [[ "$ARG" == "--project-path"* ]]; then
        HAS_PROJECT_PATH=1
        break
    fi
done

case "$COMMAND" in
    clear-console|compile|control-play-mode|execute-dynamic-code|find-game-objects|focus-window|get-hierarchy|get-logs|get-project-info|get-version|list|record-input|replay-input|run-tests|screenshot|simulate-keyboard|simulate-mouse-input|simulate-mouse-ui|sync)
        if [[ "$HAS_PROJECT_PATH" -eq 0 ]]; then
            exec npx -y uloop-cli@2.1.10 "$@" --project-path "$UNITY_PROJECT_ROOT"
        fi
        ;;
esac

exec npx -y uloop-cli@2.1.10 "$@"
