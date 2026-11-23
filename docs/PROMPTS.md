# PROMPTS.md — copy-paste prompts per phase (token-efficient)

## Token rules (for you)

1. **One phase = one session.** After merging a phase: `/clear`. Never carry a long chat into the next phase.
2. If a session gets long mid-phase: `/compact keep: phase goal, files changed, failing tests, open TODOs`.
3. **Model:** Sonnet for phases 00, 01, 02, 06, 08, 10. Opus only for 03, 05, 07, 09 (design/idempotency/HTTP).
   Switch with `/model`.
4. Don't paste the PDF, long logs, or whole files into the chat. Paste only the **first error + 10 lines**.
5. Prefer one precise prompt over many "continue"/"fix it" messages; each round trip re-sends context.
6. `/cost` (or `/usage`) at the end of each phase to see where tokens went.
7. `.claude/settings.json` already blocks reading `.env`, `bin/`, `obj/`, `App_Data/`, the PDF, and vendor libs.

---

## Phase prompts

Each prompt assumes a fresh session (`/clear`) on the right branch.
**Run order: 00 → 01 → 03 → 04 → 05 → 07 → 08 → (design) → 02 → 06 → 09 → 10.**

### 00 — bootstrap
```
Phase 00. Branch: chore/00-bootstrap (create it).
Read only the "### 00" section of docs/PLAN.md. AGENTS.md is already loaded.
nuget.config already exists at repo root (nuget.org only) — keep it.
First give me a ≤10-line plan (files + commands), wait for "ok", then implement.
Use Orchard Core 3.0.1 / .NET 10. Verify the template/package names with one `dotnet new search` or NuGet check, don't guess.
Done = docker compose up shows Orchard healthy at /health/live; format+build+test green.
Commit in 3–5 Conventional Commits. Report: changed files list + final command output only.
```

### 01 — catalog + recipe
```
Phase 01. Branch: feat/01-catalog-recipe from main.
Read only "### 01" in docs/PLAN.md and §4.1 + §8 of AGENTS.md (already loaded).
Before coding, check in Orchard 3.0 docs (one page each, max 3 pages): recipe step names for
ContentDefinition/Content/Roles/Settings, AutoSetup config keys, and whether a built-in users recipe step exists.
Write findings as 3 bullets, then implement. Add any assumption to docs/DECISIONS.md.
Keep Orchard's default theme for now (our theme comes after the UI design).
Done = fresh App_Data → auto setup → admin sees 5 services + 2 plans, can edit a price. Commit + short report.
```

### 02 — accounts + theme
```
Phase 02. Branch: feat/02-accounts-theme.
Read only "### 02" in docs/PLAN.md.
Read docs/DESIGN.md (tokens, typography, components, RTL rules) — it is the UI contract from Figma.
Theme: RTL layout, Bootstrap 5 RTL + Vazirmatn served from wwwroot (no CDN), CSS variables exactly as in DESIGN.md,
AppHeader desktop/mobile, Button/TextField/Alert styles, login card = frame D7. Override only the Users views needed
for Login/Register/ForgotPassword/ResetPassword. SMTP → smtp4dev.
Don't read vendor CSS files. Done = AC-01 works manually (list the exact steps for me to verify). Commit + short report.
```

### 03 — orders domain
```
Phase 03. Branch: feat/03-orders-domain. Use plan mode first.
Read only "### 03" in docs/PLAN.md; AGENTS.md §4.2, §4.3, §5 are the contract.
Domain project must have zero Orchard references. Implement OrderStateMachine, Order, OrderService + abstractions,
then the YesSql repository + OrderIndex + migration in the module.
Tests first for the state machine (table-driven: every valid and invalid transition) and for place/cancel rules.
Done = AC-02 proven by a test. Commit + report (≤8 lines).
```

### 04 — admin + payment
```
Phase 04. Branch: feat/04-admin-payment.
Read only "### 04" in docs/PLAN.md (admin UI = frames A1/A2 in docs/DESIGN.md). Implement Permissions, AdminMenu («ابریت»), OrdersAdminController
(list/filter/approve/cancel/retry stub), PaymentApprovalService (idempotent), MockPaymentCallbackController (HMAC, disabled if secret empty).
Also customer controllers (/services, /services/drive, /orders/new, /dashboard, /dashboard/orders/{id}) with MINIMAL unstyled
Razor views (plain semantic HTML, dir="rtl", no CSS work) — styling comes later from the design; ownership enforced server-side.
Every action: IAuthorizationService check server-side + antiforgery. Unit test: non-admin cannot approve (AC-08 at service level).
Commit + short report.
```

### 05 — provisioning (Mock) + required tests
```
Phase 05. Branch: feat/05-provisioning-mock. Use plan mode first.
Read only "### 05" in docs/PLAN.md; AGENTS.md §5 and §6 are the contract.
Implement INextcloudProvisioner, MockNextcloudProvisioner (call counters + FailNextCalls), ProvisioningRequest + repo + index,
ProvisioningService with per-order lock, single request per order, attempts cap, sanitized errors, and the admin provisioning page.
Wire approve → provision. Write the 3 REQUIRED tests with these exact names:
  Ownership_CustomerB_CannotReadCustomerA_Order
  Provisioning_ApproveTwiceAndCallback_CreatesExternalAccountOnce   (include a parallel Task.WhenAll case)
  Provisioning_Failure_SetsFailed_ThenRetry_Activates_AndRespectsMaxAttempts
Done = AC-03..AC-06 proven by tests. Commit + report.
```

### 06 — customer UI
```
Phase 06. Branch: feat/06-customer-ui.
Read only "### 06" in docs/PLAN.md, AGENTS.md §7 and docs/DESIGN.md ("Components" + "Screens → routes").
Match frames D1–D6 and M1–M3; one partial per Figma component (_StatusBadge, _ServiceCard, _PlanCard, _Alert).
Build: services list, /services/drive, order confirm, dashboard, order detail (status timeline, safe access info, Mock badge).
Jalali dates, fa-IR prices + «تومان», Persian empty/error states. Reuse one partial per card/badge; no inline styles.
Enable the "Smoke checks" step in ci.yml (remove `if: false`).
Done = I can click the full flow; give me a 6-step manual check list for 375px and 1440px. Commit + report.
```

### 07 — Real Nextcloud
```
Phase 07. Branch: feat/07-nextcloud-real. Use plan mode first.
Read only "### 07" in docs/PLAN.md and AGENTS.md §6.
Implement OcsNextcloudProvisioner (typed HttpClient, OCS headers, 100/102/error mapping, quota PUT, user GET, group create)
+ startup fail-fast when Mode=Real config is incomplete. Tests with a fake HttpMessageHandler: headers sent, 102 treated as exists,
error message never contains the app password. Add compose profile "nextcloud" (local instance) + README steps for app password.
Default mode stays Mock. Commit + report.
```

### 08 — integration tests + CI
```
Phase 08. Branch: test/08-integration.
Read only "### 08" in docs/PLAN.md.
WebApplicationFactory<Program> with temp SQLite + auto setup. Tests: AC-07 (B gets 404 on A's order), AC-08 (customer POST approve →
forbidden, order state unchanged), AC-09 (dispose host, start again on same db → order + status persist).
Keep test run < 2 min. Make sure CI build-test + docker-smoke pass. Commit + report.
```

### 09 — bonus: Team workspace
```
Phase 09 (bonus). Branch: feat/09-team-workspace.
Read only "### 09" in docs/PLAN.md. Team order → group abrit-team-{OrderId}, owner, seat cap from plan (3), 500 MB per member,
customer add/remove member by email up to the cap; same idempotency rules; Mock + Real. Tests: cap enforced, no duplicate member.
Commit + report.
```

### 10 — delivery docs
```
Phase 10. Branch: docs/10-delivery.
Read only "### 10" in docs/PLAN.md, then list the repo tree (depth 3, exclude bin/obj).
Write README (all sections listed in PLAN 10 + AGENTS §12 last bullet), docs/ARCHITECTURE.md (with a mermaid diagram of the order
flow and the 3 idempotency layers), docs/REPORT.md (done / mocked / not done / next steps — honest), and the short architecture note
on abrit.ir auth integration + WHMCS migration. Don't invent features that don't exist: grep the code to confirm each claim.
Commit + report.
```

---

## Utility prompts

**Fix a failing build/test (paste only the first error):**
```
This fails: <first error + ≤10 lines>. Find the root cause with targeted grep/read (no full-file dumps),
fix it minimally, re-run only the affected test project, report in ≤5 lines.
```

**Review before PR (cheap self-check):**
```
Review the diff of this branch vs main (`git diff main --stat`, then only changed hunks) against AGENTS.md §9 security checklist
and the PR template. List problems only (no praise), max 10 bullets, then fix them.
```

**Open PR:**
```
Push the branch and open a PR with `gh pr create` filling .github/pull_request_template.md (phase, ACs covered, checklist).
After CI is green: `gh pr merge --squash --delete-branch`.
```
