# DECISIONS.md — assumptions & technical decisions

Spec §9: "if a feature's behaviour is ambiguous, record your assumption **before** implementing it".
Format: `D-xx | decision | why | status`. Add new entries; never silently change old ones.

## Assumptions about behaviour

| ID | Assumption | Why |
|---|---|---|
| A-01 | Approving payment immediately triggers provisioning in the same request (synchronous); admin sees the result. | Simplest observable flow for AC-03/04; background queue is unnecessary at this scale. |
| A-02 | Retry is admin-only, only from `Failed`, max 5 attempts per order. The customer sees «خطا در فعال‌سازی – در حال پیگیری». | "Controlled retry" (§4.3); avoids customers hammering the external API. |
| A-03 | Cancel is allowed only in `PendingPayment` (by the owner or an admin). | Paid orders need a refund flow, which is out of scope (§5). |
| A-04 | A customer may have several orders; each order maps to at most one Drive service. | Idempotency unit = order (§4.3 "per order/subscription"). |
| A-05 | Price and quota are snapshotted on the order at creation time. | Later price edits by admin must not change existing orders. |
| A-06 | External user id = `abrit-{OrderId}` (Mock: `mock-abrit-{OrderId}`). | Deterministic id makes remote creation idempotent. |
| A-07 | No Nextcloud password is generated for display. The account is created with the customer's email; Nextcloud sends the set-password email. | §7: never show Nextcloud password in UI or logs. |
| A-08 | Team plan (required scope): display + order + provisioning of an owner account and a group. Member management = bonus (phase 09). | §3 table: real multi-user is a bonus. |
| A-09 | "Callback" in AC-05 is simulated by an HMAC-signed `POST /api/payments/mock-callback`. | There is no real gateway (§5) but AC-05 needs a repeatable callback. |
| A-10 | Accessing another customer's order returns 404, not 403. | Doesn't reveal that the order exists. |
| A-11 | UI is Persian only; dates in Jalali calendar. | §4.2 Persian RTL UI. |
| A-12 | Email confirmation on registration is off in the demo; password reset email goes to smtp4dev. | Keeps AC-01 testable locally. |

## Technical decisions

| ID | Decision | Why |
|---|---|---|
| D-01 | Orchard Core 3.0.1 on .NET 10, SQLite. | Latest stable; SQLite allowed for demo (§4.1). |
| D-02 | Service & Plan = Orchard content types, seeded by recipe. | Admin-manageable without code; uses built-in features (§4.1, §7). |
| D-03 | Order & ProvisioningRequest = YesSql documents + map indexes in our module, not content items. | Strict state machine, ownership, idempotency; must not be editable via the generic content editor. |
| D-04 | Orchard Commerce not used. | Optional in spec; its cart/checkout/payment model is heavier than this single-plan flow and would hide the logic being evaluated. |
| D-05 | Pure `Domain` project without Orchard references. | Fast, deterministic required tests; clear module boundary. |
| D-06 | Per-order lock through Orchard `IDistributedLock` + single ProvisioningRequest + deterministic external id. | Three-layer idempotency (see ARCHITECTURE.md). |
| D-07 | Server-rendered Razor + Bootstrap 5 RTL, no SPA. | One deployable; uses Orchard theming; spec asks not to rebuild built-in systems. |
| D-08 | Admin user via Orchard AutoSetup from env vars; demo customers via `DemoUsers` recipe step. | No repeated manual entry (§3) and no secrets in git (§7). |
| D-09 | Seeded catalog uses fixed ContentItemIds (`abrsvc-drive`, `abrsvc-erp360`, `abrsvc-desk`, `abrsvc-itsupport`, `abrplan-drive-starter`, `abrplan-drive-team`), Published + Latest. | The `AbrPlan.Service` ContentPicker must resolve on a fresh setup and later phases/orders reference plans by stable id; re-running the recipe updates instead of duplicating. Extends D-02. |
| D-10 | Recipe feature IDs verified against the 3.0.1 assemblies: `OrchardCore.Users.Registration`, `OrchardCore.Users.ResetPassword`, `OrchardCore.Email.Smtp`, etc. all exist unchanged; Roles step is omitted (Orchard creates default roles; phase 04 permissions reach Administrator via stereotypes). `desk`/`it-support` MoreInfoUrl points to `https://abrit.ir` until exact pages are known. | Avoids guessing feature names; no custom permissions yet. |
