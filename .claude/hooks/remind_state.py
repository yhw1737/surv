#!/usr/bin/env python3
"""
Stop hook — nudge Claude when code changed but PROJECT_STATE.md did not.

Reads the Stop payload on stdin. Emits hookSpecificOutput.additionalContext
when Assets/ code or definition files are dirty while docs/PROJECT_STATE.md
is untouched. Silent otherwise. Respects stop_hook_active to avoid loops.
"""
import json
import subprocess
import sys


def changed_files():
    try:
        out = subprocess.run(
            ["git", "status", "--porcelain"],
            capture_output=True, text=True, timeout=5,
        )
        if out.returncode != 0:
            return None
        return [l[3:].strip().strip('"') for l in out.stdout.splitlines() if len(l) > 3]
    except Exception:
        return None


def main():
    try:
        payload = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    if payload.get("stop_hook_active"):
        sys.exit(0)

    files = changed_files()
    if files is None:
        sys.exit(0)

    code_touched = any(
        f.startswith("Assets/") and (f.endswith(".cs") or f.endswith(".json"))
        for f in files
    )
    state_touched = any("PROJECT_STATE.md" in f for f in files)

    if code_touched and not state_touched:
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "Stop",
                "additionalContext": (
                    "Code files changed but docs/PROJECT_STATE.md was not updated. "
                    "If you are wrapping up, follow the wrap skill: update "
                    "PROJECT_STATE.md and BACKLOG.md, and record any values you "
                    "decided without a spec. Do not commit. "
                    "If you are still mid-task, ignore this and continue. "
                    "Reply to the developer in Korean."
                ),
            }
        }))
    sys.exit(0)


if __name__ == "__main__":
    main()
