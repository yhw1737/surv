---
name: wrap
description: Close out a work session. Review changes, update PROJECT_STATE.md and BACKLOG.md, then hand off to the developer for verification. Does not commit. Use when ending a session, or when the user says "wrap", "wrap up", "finish up", or runs /wrap.
---

# Session wrap-up

Record what happened so the next session can pick up the thread.
**Never ask the developer to update the state docs — do it yourself.**

> ⚠️ **This skill does not commit.** Verification is the developer's job (CLAUDE.md Git rules §3).

## Steps

**1. Find what changed**
```bash
git status --short
git diff --stat
git branch --show-current
```
Base this on **actual file changes, not conversation memory.**

**2. Check tests**
If EditMode tests were added, confirm they pass. If not yet run, ask the developer to run them and record the result.

**3. Update `docs/PROJECT_STATE.md`** — fill every field, leave none blank:
header (date, phase, task, branch, pending commit) · progress counts · completed (task ID, one line, files, verification) · in progress (how far, what remains) · next task ID · **decisions made without a spec** · blockers · pending commits table · gate status

**4. ★ Record spec-less decisions**

Any number or design choice made because the spec didn't cover it:
```markdown
## Decided without a spec
- [T-043] Stack split defaults to half. SYS-INV-01 §Required UX doesn't specify
  → needs to go into the spec
```
This debt makes balancing impossible if it accumulates. **If there's even one, say so out loud in the final report.**

**5. Update `docs/BACKLOG.md`** — `[x]` done, `[~]` in progress, `[!]` blocked

**6. Offer to pay down spec debt**

If step 4 found anything:
> "스펙에 없어서 임의로 정한 게 N개 있습니다. 지금 스펙 시트에 반영할까요?"

On approval, update the relevant `docs/specs/SYS-*.md` and its verification cases.

**7. ★ Stop without committing**

No `git add`, `git commit`, `git push`, or PR.

The developer runs and verifies the work, then requests a commit explicitly:
- ✅ "commit this", "open a PR" → proceed then
- ❌ "done?", "nice", "thanks" → not a commit request

When asked to commit, follow CLAUDE.md Git rules §4–5. **Never include Co-Authored-By, "Generated with", emoji signatures, or any AI attribution.**

## Final report — in Korean

Four lines. It's the end of a session — keep it short.
Documents are English, but **the report to the developer is Korean** (CLAUDE.md §Language).

```
✅ T-0XX 완료 — 한 줄 요약
🌿 feature/T-0XX-...  (커밋 안 함, 검증 대기)
📋 다음: T-0YY (태스크 이름)
⚠️  확인 필요: (있을 때만)
```
