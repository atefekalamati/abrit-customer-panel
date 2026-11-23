# PLAN.md — delivery plan

Deadline: **Wednesday 4 Azar 1405** (spec deadline: Thursday 5 Azar). Branching: GitHub Flow —
`main` is protected and always runnable; one branch per phase; merge by PR only after CI is green.

## Phase overview

Order changed: **backend first** (00, 01, 03, 04, 05, 07, 08) → **UI design** (D) → **frontend** (02, 06) → bonus (09) → delivery (10).
Until phase 02, Orchard's default theme is used; customer pages exist with minimal unstyled views.

| Order | # | Branch | Goal | Day | Status |
|---|---|---|---|---|---|
| 1 | 00 | `chore/00-bootstrap` | solution, Orchard host runs, CI skeleton, Docker, README stub | Sun | ☐ |
| 2 | 01 | `feat/01-catalog-recipe` | content types + seed recipe + AutoSetup + demo users (default theme) | Sun | ☐ |
| 3 | 03 | `feat/03-orders-domain` | Domain project, state machine, Order + repos + indexes, place/cancel order | Mon | ☐ |
| 4 | 04 | `feat/04-admin-payment` | permissions, admin orders, approve/cancel, mock callback, **customer controllers with minimal views** | Mon | ☐ |
| 5 | 05 | `feat/05-provisioning-mock` | connector, Mock, ProvisioningService, idempotency, Failed/Retry + **3 required tests** | Mon | ☐ |
| 6 | 07 | `feat/07-nextcloud-real` | OCS client + tests, local Nextcloud compose profile | Tue | ☐ |
| 7 | 08 | `test/08-integration` | WebApplicationFactory tests AC-07/08/09 + CI docker smoke (health only) | Tue | ☐ |
| 8 | D | — (design, no code) | finalize the existing draft (`design-system/`, `docs/prototype/`, Figma, `docs/DESIGN.md`) and sign off | Tue | ◐ draft exists |
| 9 | 02 | `feat/02-accounts-theme` | implement theme from the design: RTL layout, tokens, auth views, smtp4dev | Tue | ☐ |
| 10 | 06 | `feat/06-customer-ui` | restyle customer pages from the design, responsive, enable full CI smoke | Tue–Wed | ☐ |
| 11 | 09 | `feat/09-team-workspace` | **bonus**: Team group, owner, seat cap, per-member quota | Wed | ☐ |
| 12 | 10 | `docs/10-delivery` | README, ARCHITECTURE, REPORT, screenshots/video AC-02..06 | Wed | ☐ |

Rule: if the clock runs out, skip 09 first, then trim 07 to "implemented + unit-tested, not claimed as connected".

## Phase details

### 00 — bootstrap
- `global.json` (.NET 10), `AbrIT.Panel.sln`, `src/AbrIT.Panel.Web` from `dotnet new occms` (template `OrchardCore.ProjectTemplates` 3.0.1) **or** empty web + `OrchardCore.Application.Cms.Targets` 3.0.1.
- Empty projects: Domain, Nextcloud, Module (`OrchardCore.Module.Targets`), Theme (`OrchardCore.Theme.Targets`), 3 test projects.
- `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true` for our projects, `LangVersion=latest`.
- `.editorconfig`, `.gitignore`, `.env.example`, Dockerfile, `docker-compose.yml` (app + smtp4dev), `ci.yml`, `cd.yml`.
- Enable `OrchardCore.HealthChecks` → `/health/live` used by CI smoke.
- ✅ Done when: `docker compose up` shows Orchard running; CI green on the PR.

### 01 — catalog + recipe
- Recipe with `feature`, `ContentDefinition` (AbrService, AbrPlan), `Content` (5 services, 2 plans), `Roles`, `Settings`.
- AutoSetup from env (site name, admin user/email/password, recipe name, SQLite).
- `DemoUsers` recipe step (customer A, B) from env; skipped if passwords empty.
- ✅ Done when: fresh `App_Data` + `docker compose up` → admin can log in, sees services & plans in Content, edits a price.

### D — UI design (before 02)
- Draft already in repo: `design-system/` (tokens.css = single source), `docs/prototype/index.html` (all screens, desktop + mobile),
  `docs/DESIGN.md` (UI contract), `docs/SCREENS.md`, Figma file linked in DESIGN.md.
- To finish: review every screen (services, Drive plans, order confirm, dashboard, order detail in all 6 statuses + Mock badge,
  login/register/forgot/reset, admin A1/A2) at 375 and 1440, set final brand colors, sign off. Backend phases do not depend on it.

### 02 — accounts + theme
- Theme: `Layout.cshtml` with `<html lang="fa" dir="rtl">`, Bootstrap 5 RTL + Vazirmatn in `wwwroot` (no CDN), header (logo text, nav, login state), footer.
- Colors, type, components: exactly from `design-system/tokens.css` and `docs/DESIGN.md` (signed off in phase D).
- Override Users views (Login, Register, ForgotPassword, ResetPassword) for Persian RTL.
- SMTP → smtp4dev; document reset flow.
- ✅ AC-01 manually verified; screenshot.

### 03 — orders domain
- `AbrIT.Panel.Domain`: `OrderStatus`, `Order`, `StatusChange`, `OrderStateMachine`, `IOrderService`/`OrderService`, abstractions.
- Module: `YesSqlOrderRepository`, `OrderIndex`, migrations; `IOrderLock` via `IDistributedLock`.
- Unit tests: state machine (all valid + invalid transitions), place order snapshots price, cancel rules.
- ✅ AC-02 proven by test.

### 04 — admin + payment
- Customer controllers (`/services`, `/services/drive`, `/orders/new`, `/dashboard`, `/dashboard/orders/{id}`) with **minimal unstyled Razor views** (plain HTML, `dir="rtl"`), ownership enforced server-side. Phase 06 only restyles them.
- `Permissions.cs`, `AdminMenu.cs`, `OrdersAdminController` (list, filter by status, approve, cancel, retry), views.
- `PaymentApprovalService` (idempotent), `MockPaymentCallbackController` with HMAC.
- ✅ AC-03 (without provisioning yet → stays Paid), AC-08 unit-level.

### 05 — provisioning (Mock)
- `AbrIT.Panel.Nextcloud`: `INextcloudProvisioner`, `MockNextcloudProvisioner`, options, DI by `Nextcloud:Mode`.
- `ProvisioningRequest` + repo + index; `ProvisioningService` (lock, one request per order, attempts, sanitized errors).
- Admin provisioning page: mode (read-only), mock failure toggle.
- **Required tests**: (1) ownership: B gets null for A's order; (2) idempotency: approve twice / parallel approve + callback → create called once; (3) failure → Failed, retry → Active, attempts capped.
- ✅ AC-03..AC-06 proven by tests.

### 06 — customer UI
- Services list cards, Drive detail with plans, order confirm, dashboard, order detail with status timeline + safe access info + Mock badge.
- Jalali dates, `fa-IR` prices, empty states, error messages in Persian.
- ✅ AC-10: check at 375px and 1440px; screenshots.

### 07 — Real Nextcloud
- `OcsNextcloudProvisioner` + tests with fake handler (headers, 100/102/error mapping, no secret in error text).
- Compose profile `nextcloud` (nextcloud:stable + sqlite) with admin from env; README: create app password, set env, switch Mode.
- If AbrIT provides a test Nextcloud: use only that one for "Real" claims.
- ⚠ Spec §9 says: without an AbrIT-provided Nextcloud, use Mock. Before relying on the local docker Nextcloud,
  ask AbrIT in writing whether testing Real mode against our own local instance is acceptable. Until they answer,
  label it in README/REPORT as "verified against a local Nextcloud we ran ourselves — not an AbrIT environment",
  and keep Mock as the default mode everywhere (demo, CI, video).
- ✅ Real create + quota + read status shown working against local instance, documented honestly.

### 08 — integration + CI
- `WebApplicationFactory<Program>` with temp SQLite + auto setup; tests: B → 404 on A's order (AC-07), customer POST approve → forbidden & no state change (AC-08), restart host → data persists (AC-09).
- CI docker smoke job.

### 09 — bonus: Team workspace
- Team order → group `abrit-team-{OrderId}`, owner account, seat cap 3, per-member 500 MB, customer can add/remove member (by email) up to cap; Mock + Real.

### 10 — delivery
- README: prerequisites, setup from zero, seed method, test admin login, Mock/Real config, running tests, Docker.
- README section **"Login link from abrit.ir — technical proposal"** (spec §4.2: documented only, main site untouched).
- README section **"How to add a new service"** (spec §7): add AbrService/AbrPlan content (or recipe), implement a connector behind an interface, register it by `ServiceKey`.
- README section **"Assumptions"** linking `docs/DECISIONS.md` + the GitHub Issues opened for each assumption (spec §9: Issue/README).
- `docs/ARCHITECTURE.md` (+ diagram), `docs/REPORT.md` (done / mocked / not done / next steps), short architecture note on future abrit.ir auth integration + WHMCS migration (bonus §12).
- 3–5 min video or screenshots AC-02 → AC-06. Repo private; add evaluator as read collaborator.

## GitHub setup (once)

```bash
# repo already created: https://github.com/atefekalamati/abrit-customer-panel (private)
git remote add origin https://github.com/atefekalamati/abrit-customer-panel.git
git push -u origin main
# branch protection on main: require PR + passing "CI / build-test" and "CI / docker-smoke"
gh api -X PUT repos/atefekalamati/abrit-customer-panel/branches/main/protection \
  -F required_status_checks[strict]=true \
  -F 'required_status_checks[contexts][]=build-test' \
  -F 'required_status_checks[contexts][]=docker-smoke' \
  -F enforce_admins=false -F required_pull_request_reviews=null -F restrictions=null
# per phase
git switch -c feat/03-orders-domain   # ... work, commit ...
gh pr create --fill && gh pr merge --squash --delete-branch   # after CI is green
```
Note: branch protection on **private** repos needs GitHub Pro/Team. If unavailable, keep the PR discipline anyway.
Also open one GitHub Issue per assumption in DECISIONS.md (spec §9 allows Issue or README).
