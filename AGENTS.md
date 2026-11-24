# AGENTS.md — AbrIT Customer Panel (Orchard Core hiring task)

> Single source of truth for any coding agent (Claude Code, Copilot, Codex, Cursor) working in this repo.
> The task spec is `docs/TASK_SPEC.pdf` (AbrIT Orchard Core Hiring Task v2) — this file already summarizes it;
> don't read the PDF unless the user asks (token budget). **The spec wins** over this file
> except where following it literally would introduce a bug or security hole — in that case record the
> deviation in `docs/DECISIONS.md` before writing code.

## 1. What we are building

A runnable **sample customer panel for AbrIT** built on **Orchard Core CMS**, with a testable, isolated
connector to **Nextcloud** (Mock + Real modes). It is an evaluation sandbox: it must never touch
`abrit.ir`, `my.abrit.ir`, WHMCS, or any production Nextcloud/database.

Main user story (primary success criterion — build this end-to-end first):

1. Customer registers / logs in.
2. Sees the service list, picks an **AbrIT Drive** plan.
3. An order is created in `PendingPayment` ("در انتظار تأیید پرداخت").
4. A test **admin** confirms payment (simulated — no real gateway).
5. System provisions a Drive account through the connector, sets quota, records result.
6. Customer dashboard shows order status, activation status, plan, and **safe** access info.
7. API errors or repeated requests never create a duplicate service; Failed state + controlled Retry are visible.

Priority order (from spec §11): **full main scenario → visual quality → bonus features.**
An honest, runnable, partially complete result beats a demo that fakes behaviour.

## 2. Tech stack (fixed)

| Area | Choice |
|---|---|
| Runtime | .NET 10 SDK (pinned in `global.json`) |
| CMS / framework | **Orchard Core 3.0.1** (`OrchardCore.Application.Cms.Targets`) — record version in README |
| DB | SQLite (persistent file in `App_Data`, Docker volume) |
| Data access | YesSql (Orchard's store) for custom documents + map indexes |
| UI | Server-rendered Razor views (theme + module), Bootstrap 5 **RTL**, Vazirmatn font (self-hosted) |
| Auth | Orchard Core `Users` module (Registration, Login, Reset Password) + Roles/Permissions |
| Email (dev) | Orchard SMTP → **smtp4dev** container (or pickup directory when running without Docker) |
| Tests | xUnit (+ `Microsoft.AspNetCore.Mvc.Testing` for integration tests). No FluentAssertions (license) |
| Container | Dockerfile (multi-stage) + `docker-compose.yml` (app, smtp4dev, optional nextcloud profile) |
| CI/CD | GitHub Actions: `ci.yml` (format, build, test, docker smoke), `cd.yml` (image → GHCR) |

There is **no separate SPA frontend**. Backend (C# module) and frontend (Razor views + theme assets) live in the
same Orchard Core app — this is what the spec asks for ("use Orchard Core built-in capabilities").

## 3. Solution layout

```
AbrIT.Panel.sln
global.json
src/
  AbrIT.Panel.Web/            # Orchard Core CMS host (Program.cs, appsettings, AutoSetup, HealthChecks)
  AbrIT.Panel.Domain/         # Pure C#: entities, OrderStateMachine, services, abstractions. NO Orchard refs.
  AbrIT.Panel.Nextcloud/      # INextcloudProvisioner impls: MockNextcloudProvisioner, OcsNextcloudProvisioner
  AbrIT.Panel.Module/         # Orchard module: YesSql repos/indexes/migrations, controllers, views,
                              #   permissions, admin menu, recipes, DI wiring (Startup.cs, Manifest.cs)
  AbrIT.Theme/                # Orchard theme: RTL layout, Bootstrap RTL, Vazirmatn, brand CSS variables
tests/
  AbrIT.Panel.Domain.Tests/       # ownership, idempotency, state machine, retry (fast, no I/O)
  AbrIT.Panel.Nextcloud.Tests/    # OCS client against fake HttpMessageHandler
  AbrIT.Panel.Web.Tests/          # integration: WebApplicationFactory + temp SQLite (authZ, persistence)
docs/
  TASK_SPEC.pdf  PLAN.md  DECISIONS.md  ARCHITECTURE.md  REPORT.md  screenshots/
.github/workflows/ci.yml  cd.yml
Dockerfile  docker-compose.yml  .env.example  README.md
```

Rules:
- **Never modify Orchard Core source** (spec §7). All custom code lives in our module/theme/libraries.
- `Domain` must not reference Orchard Core or ASP.NET — that is what keeps the required tests fast and honest.
- Don't rebuild what Orchard already has (users, roles, content editing, recipes, email, health checks).

## 4. Domain model

### 4.1 Catalog — Orchard **Content Types** (admin-manageable, seeded by recipe)

`AbrService` — TitlePart, AutoroutePart, MarkdownBodyPart, fields:
`ServiceKey` (Text: `drive`, `erp360`, `desk`, `it-support`), `Availability` (Text, values `Orderable` | `ComingSoon` | `InfoOnly`),
`MoreInfoUrl` (Link), `Icon` (Text), `SortOrder` (Numeric).

`AbrPlan` — TitlePart, fields: `PlanKey` (Text: `drive-starter`, `drive-team`), `Service` (ContentPicker → AbrService),
`PriceToman` (Numeric), `BillingPeriod` (Text: `Monthly`), `QuotaPerUserMB` (Numeric), `MaxUsers` (Numeric),
`IsOrderable` (Boolean), `Description` (Text).

Seed data (spec §3 — sample values, not real tariffs):

| Service | Plan | Values | Behaviour |
|---|---|---|---|
| AbrIT Drive | Starter | 1 account, 1024 MB, 99,000 Toman/month | order + **Mock activation required** |
| AbrIT Drive | Team | 3 users × 500 MB, 299,000 Toman/month | display + order required; real multi-user = bonus |
| ERP360 | — | intro card | badge «به‌زودی» |
| AbrIT Desk | — | intro card | «اطلاعات بیشتر» link |
| پشتیبانی IT | — | intro card | link to the related page on abrit.ir |

Why content types (write this in DECISIONS.md): catalog is editorial content that admins must edit without a
deploy; Orchard gives editor UI, versioning, recipes, and permissions for free.

### 4.2 Transactional data — YesSql **documents** (not content items)

Why: orders have a strict state machine, ownership, concurrency and idempotency rules that must be enforced
server-side; they are not editorial content and must not be editable through the generic content editor.

`Order`
- `OrderId` (string, `IdGenerator`), `OrderNumber` (human readable, e.g. `ABR-1405-000123`)
- `OwnerUserId`, `OwnerUserName`
- `PlanContentItemId`, `PlanSnapshot { PlanKey, Title, PriceToman, QuotaPerUserMB, MaxUsers }` — prices frozen at order time
- `AmountToman`, `Status` (`OrderStatus`), `PaymentStatus` (`Pending` | `ConfirmedSimulated`)
- `CreatedUtc`, `PaidUtc`, `PaidByUserId`, `CancelledUtc`, `ActivatedUtc`
- `History: List<StatusChange { From, To, AtUtc, ActorUserId, Note }>`

`ProvisioningRequest` (exactly **one per order**)
- `RequestId`, `OrderId`, `IdempotencyKey = "order:{OrderId}"`
- `Mode` (`Mock` | `Real`), `ExternalUserId` (deterministic: `abrit-{OrderId}`), `ExternalGroupId?` (Team)
- `QuotaBytes`, `Status` (`Pending` | `InProgress` | `Succeeded` | `Failed`)
- `Attempts`, `MaxAttempts` (default 5), `LastAttemptUtc`, `LastErrorCode`, `LastErrorMessage` (**sanitized**, no secrets)

Map indexes: `OrderIndex(OrderId, OwnerUserId, Status, CreatedUtc)`, `ProvisioningRequestIndex(OrderId, Status)`.
Migrations in the module's `Migrations.cs`.

### 4.3 Order state machine (`OrderStateMachine`, pure, unit-tested)

```
PendingPayment ──approve(admin)──▶ Paid ──start──▶ Provisioning ──ok──▶ Active
      │                                                 │
      └──cancel──▶ Cancelled                            └──error──▶ Failed ──retry(admin)──▶ Provisioning
```
- Any transition not in this graph throws `InvalidOrderTransitionException`.
- `Cancelled` and unpaid orders can **never** reach provisioning.
- Approving an already `Paid/Provisioning/Active` order is a **no-op that returns the current state** (idempotent), not an error.

Persian labels: PendingPayment «در انتظار تأیید پرداخت», Paid «پرداخت‌شده», Provisioning «در حال فعال‌سازی»,
Active «فعال», Failed «خطا در فعال‌سازی», Cancelled «لغوشده». In Mock mode Active is shown as
«فعال (آزمایشی – Mock)» (wrap "Mock" in <bdi>) — **never** as a real active service (spec §7).

## 5. Services (Domain)

- `IOrderService`
  - `PlaceOrderAsync(userId, planContentItemId)` → validates plan is orderable, snapshots price, status `PendingPayment`.
  - `GetForOwnerAsync(userId, orderId)` → returns `null` when the order belongs to someone else (controller returns **404**, not 403, to avoid leaking existence).
  - `ListForOwnerAsync(userId)`; `CancelAsync(userId, orderId)` (only own + only `PendingPayment`).
- `IPaymentApprovalService.ApproveAsync(orderId, adminUserId)` — admin-only; idempotent; triggers provisioning.
- `IProvisioningService`
  - `ProvisionAsync(orderId)` — under a **per-order lock** (`order:{id}`): load/create the single
    `ProvisioningRequest`; if `Succeeded` → return; if `InProgress` → return; else call connector.
  - `RetryAsync(orderId, adminUserId)` — only from `Failed`, only while `Attempts < MaxAttempts`.
- Abstractions implemented in the module: `IOrderRepository`, `IProvisioningRequestRepository`,
  `IOrderLock` (wraps Orchard `IDistributedLock`), `IClock`, `ICurrentModeProvider`.

Idempotency is defended in **three layers** (document in ARCHITECTURE.md):
1. state machine (only `Paid`/`Failed` can enter provisioning),
2. one `ProvisioningRequest` per order created inside the per-order lock,
3. deterministic external id + "exists?" check at the connector (Real: OCS returns 102 = already exists → treat as success, then set quota).

## 6. Nextcloud connector

```csharp
public interface INextcloudProvisioner
{
    ProvisionerMode Mode { get; }
    Task<ProvisionResult> EnsureUserAsync(EnsureUserCommand cmd, CancellationToken ct);   // create-if-missing
    Task<ProvisionResult> SetQuotaAsync(string externalUserId, long quotaBytes, CancellationToken ct);
    Task<ExternalUserStatus?> GetUserAsync(string externalUserId, CancellationToken ct);
    Task<ProvisionResult> EnsureGroupAsync(string groupId, CancellationToken ct);        // Team / bonus
}
```

**Mock** (`MockNextcloudProvisioner`): no network, no keys. In-memory store + call counters (tests assert exactly
one create). Failure injection for AC-06: admin page toggle «شبیه‌سازی خطا در فراخوانی بعدی» and config
`Nextcloud:Mock:FailNextCalls` (int). Returns fake ids like `mock-abrit-{OrderId}`.

**Real** (`OcsNextcloudProvisioner`) — OCS Provisioning API via typed `HttpClient`:
- Headers: `OCS-APIRequest: true`, `Accept: application/json`, Basic auth `AdminUser:AppPassword`.
- Create: `POST {BaseUrl}/ocs/v1.php/cloud/users?format=json` form `userid, email, displayName` (no password →
  Nextcloud sends the welcome / set-password email). If password policy requires one, generate a random one and
  **discard it** — never store, log or display it.
- Quota: `PUT /ocs/v1.php/cloud/users/{userid}` form `key=quota&value={bytes}`.
- Read: `GET /ocs/v1.php/cloud/users/{userid}`. Group: `POST /ocs/v1.php/cloud/groups`, `POST .../users/{id}/groups`.
- Map OCS `meta.statuscode` (100 ok; 102 exists; others → error) — test with a fake `HttpMessageHandler`.
- Timeout + 1 transient retry max (Polly / `Microsoft.Extensions.Http.Resilience`); the controlled business retry is separate.

Config (env vars / user-secrets only, never committed):
`Nextcloud__Mode` (`Mock` default), `Nextcloud__BaseUrl`, `Nextcloud__AdminUser`, `Nextcloud__AppPassword`,
`Nextcloud__TimeoutSeconds`. If `Mode=Real` and config is incomplete → app fails fast at startup with a clear error.
Real mode is only ever pointed at a Nextcloud **provided by AbrIT** or our **local docker** instance — say which in README.
Spec §9: without an AbrIT-provided instance, the official mode is **Mock**; the local instance is only an extra
verification and must never be presented as "connected to AbrIT Nextcloud".

## 7. Web surface

Customer (theme layout, `dir="rtl" lang="fa"`):
| Route | Page |
|---|---|
| `/` → `/services` | services list (cards; Drive orderable, ERP360 «به‌زودی», Desk «اطلاعات بیشتر», IT support → abrit.ir) |
| `/services/drive` | Drive detail + plans (Starter, Team) |
| `/orders/new?plan={id}` | order review/confirm (POST + antiforgery) |
| `/dashboard` | my orders + statuses |
| `/dashboard/orders/{orderId}` | order/service detail, activation status, safe access info |
| Orchard routes | `/Login`, `/Register`, `/ForgotPassword`, `/LogOff` (restyled in theme) |

"Safe access info" = Drive URL, external username, mode badge, quota, instructions to set password via the
Nextcloud email. **Never** a password or token.

Admin (Orchard admin, `[Admin]` controllers, admin menu «ابریت»):
| Route | Permission |
|---|---|
| `/Admin/AbrIT/Orders` list + filters | `ViewAllOrders` |
| `POST /Admin/AbrIT/Orders/{id}/ApprovePayment` | `ApproveTestPayments` |
| `POST /Admin/AbrIT/Orders/{id}/Retry` | `ApproveTestPayments` |
| `POST /Admin/AbrIT/Orders/{id}/Cancel` | `ApproveTestPayments` |
| `/Admin/AbrIT/Provisioning` (mode read-only, mock failure toggle) | `ManageProvisioning` |

Simulated payment callback (for AC-05): `POST /api/payments/mock-callback` with `{orderId}` and header
`X-Signature: HMAC-SHA256(body, MockPayment__WebhookSecret)`. Disabled when the secret is empty.
Goes through the same idempotent `ApproveAsync`.

Permissions are defined in `Permissions.cs` and granted to the `Administrator` role in the recipe;
`Authenticated` gets none of them. **Every** check is server-side via `IAuthorizationService`.

UI contract: `docs/DESIGN.md` (tokens, components, screens from the Figma file). Read it only in UI phases (02, 04, 06).

## 8. Recipes & seeding (no repeated manual entry)

- `src/AbrIT.Panel.Module/Recipes/abrit-panel.recipe.json` (`IsSetupRecipe: true`): `feature` step
  (Users, Users.Registration, Users.ResetPassword, Email, Email.Smtp, Roles, Contents, ContentTypes,
  Title, Autoroute, Markdown, ContentFields, Themes, HealthChecks, our module + theme), `ContentDefinition`,
  `Content` (services + plans), `Roles`, `Settings` (registration open, email confirmation off for demo, theme).
- Admin user via **Orchard AutoSetup** (`OrchardCore.AutoSetup`) from env vars — not stored in the recipe.
- Demo customers A and B: custom recipe step `DemoUsers` that reads `DEMO_CUSTOMER_A_PASSWORD` /
  `DEMO_CUSTOMER_B_PASSWORD`; skipped when empty. (Check first whether Orchard 3.0 has a built-in users step.)

## 9. Security checklist (spec §7) — must hold in every PR

- [ ] No secrets in git. Only `.env.example` with empty values. Use env vars or `dotnet user-secrets`.
- [ ] Server-side authorization on every admin action; customers only query by `OwnerUserId == currentUserId`.
- [ ] Antiforgery on all POSTs. Input validated (plan exists + orderable, ids well-formed).
- [ ] Nextcloud password/app-password never in UI, logs, exceptions, or `LastErrorMessage`.
- [ ] Real/Mock boundary visible in UI; nothing claims a real activation that did not happen.
- [ ] Structured logging (`ILogger`), no PII beyond user id/order id.

## 10. Acceptance criteria → where each is proven

| AC | Proof |
|---|---|
| AC-01 auth + reset | `AC01_Register_Login_Logout_ForgotPassword_Reset_LoginWithNewPassword` (captured e-mail); manual: smtp4dev / `App_Data/mail` |
| AC-02 A orders Starter → PendingPayment | `AC02_CustomerA_OrdersStarter_IsPendingPayment_WithOwnerAndAmount` + Domain test + screenshot |
| AC-03 admin approves → Paid → Provisioning/Active | `AC03_AC04_AdminApproves_BecomesActiveMock_With1GiBQuota` + Domain test + screenshot |
| AC-04 Mock creates fake account + 1 GB quota | `AC03_AC04_AdminApproves_BecomesActiveMock_With1GiBQuota` (quota bytes = 1 073 741 824) + Domain test |
| AC-05 double approve/callback → no 2nd service | `AC05_DoubleApproveAndCallback_CreatesOneExternalAccount` (create count == 1) + **required** Domain idempotency test |
| AC-06 error → Failed + safe Retry | `AC06_ProvisioningFailure_SetsFailed_RetryActivates` + **required** Domain failure/retry test |
| AC-07 B cannot see A's order | `AC07_CustomerB_GetsNotFound_OnCustomerAOrder` (404) + **required** Domain ownership test |
| AC-08 non-admin approve → denied | `AC08_NonAdmin_ApprovePost_IsDenied_AndOrderUnchanged` (403/redirect, no state change) |
| AC-09 restart keeps data | `AC09_RestartHost_SameAppData_OrderAndStatusPersist` (dispose host, start again on same SQLite) |
| AC-10 mobile + desktop RTL | Manual check at 375px & 1440px; screenshots in `docs/screenshots/` |

## 11. Commands

```bash
dotnet restore && dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/AbrIT.Panel.Web          # http://localhost:5000
docker compose up --build                         # app :8080, smtp4dev UI :5080
docker compose --profile nextcloud up             # + local Nextcloud :8081 for Real-mode testing
```

## 12. Working rules for agents

- Read `docs/PLAN.md` and work **one phase at a time** on its branch. Don't start a later phase early.
- Before implementing anything ambiguous: add an entry to `docs/DECISIONS.md` (spec §9 requires recording assumptions first).
- Small, meaningful commits — Conventional Commits (`feat(orders): ...`, `test(provisioning): ...`, `ci: ...`).
- Every phase ends green: `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`.
- Write the test together with the behaviour (required tests must exist by the end of phase 05).
- Don't add packages without a reason written in the PR description. Prefer what Orchard already ships.
- UI strings in Persian; code, identifiers, commits, and docs in English. Dates shown in Jalali (`PersianCalendar`), prices with `fa-IR` digit grouping + «تومان».
- If something can't be finished, ship the runnable state and write it honestly in `docs/REPORT.md`.
- Final README must also contain (spec §4.2, §7, §9): login-link-from-abrit.ir proposal (docs only),
  "How to add a new service", known limitations / unimplemented features, and a link to the assumptions.
