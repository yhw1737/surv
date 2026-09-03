#!/usr/bin/env python3
"""
PreCompact hook — force a state write before context compaction.

Compaction happens in long sessions and loses detail. Writing
PROJECT_STATE.md first preserves continuity across the compaction.
"""
import json
import sys

try:
    json.load(sys.stdin)
except Exception:
    sys.exit(0)

try:
    print(
        "[notice] Context compaction is about to occur; session detail may be lost. "
        "Record current progress and any spec-less decisions in "
        "docs/PROJECT_STATE.md now."
    )
    sys.stdout.flush()
except BrokenPipeError:
    pass
sys.exit(0)
