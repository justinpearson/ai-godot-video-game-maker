#!/usr/bin/env bash
# Reports Claude Code busy/idle status to a local status endpoint.
# Usage: bash .claude/hooks/claude-status.sh busy|idle
STATUS="${1:?Usage: claude-status.sh busy|idle}"
curl -s -X POST "http://localhost:8080/claude-status" \
    -H "Content-Type: application/json" \
    -d "{\"status\":\"$STATUS\"}" \
    --connect-timeout 2 \
    -o /dev/null 2>/dev/null || true
