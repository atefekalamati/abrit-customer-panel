# CLAUDE.md

@AGENTS.md

## Claude Code specifics

- `docs/PLAN.md` and `docs/DECISIONS.md` are NOT auto-loaded (token budget). Read only the section of the
  current phase from PLAN.md (`grep -n "### 0X" docs/PLAN.md` then read that range), and DECISIONS.md only
  when you need to add/check an assumption.
- Never read `docs/TASK_SPEC.pdf` unless I ask — AGENTS.md already summarizes it.
- Quiet commands only:
  - build: `dotnet build -v q -clp:ErrorsOnly --nologo`
  - test: `dotnet test --nologo -v q 2>&1 | tail -n 30`
  - format: `dotnet format --verify-no-changes -v q`
  - logs: always `| tail -n 40` or `grep`; never dump whole logs or generated files.
- Search with `grep -rn` / `rg` scoped to `src/` or `tests/`; don't open files you don't need. Read big files by line range.
- Don't re-read a file you just wrote. Don't paste whole files back to me — summarize what changed in ≤ 8 lines.
- When an Orchard Core 3.0 API is uncertain, check https://docs.orchardcore.net/en/stable/ (one targeted page), don't guess.
- Before saying a phase is done: format + build + test must pass; show only the final summary lines.
- Never print, echo, or commit values from `.env` or user-secrets.
