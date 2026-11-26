# AbrIT Customer Panel (Orchard Core sample)

A runnable sample customer panel for AbrIT: a customer registers, orders an **AbrIT Drive** plan, an admin confirms a
**simulated** payment, and the system provisions a Drive account through an isolated Nextcloud connector. The UI is
Persian, right-to-left. Built on **Orchard Core 3.0.1** / .NET 10 with SQLite.

It is an evaluation sandbox. It never touches abrit.ir, my.abrit.ir, WHMCS or any production system, takes no real
payment, and in the default **Mock** mode creates no real Drive account.

| Document | Content |
|---|---|
| [docs/REPORT.md](docs/REPORT.md) | What is done, what is mocked, what is missing, evidence for the acceptance criteria |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Projects, order flow, idempotency, security boundaries |
| [docs/FUTURE.md](docs/FUTURE.md) | What would come next with more time |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Assumptions and technical decisions |
| [docs/evidence/](docs/evidence/) | Screenshots and run log for AC-02 to AC-06 |
| [docs/screenshots/review/](docs/screenshots/review/) | Screenshots of the final UI, desktop and mobile |
| [docs/DESIGN.md](docs/DESIGN.md), [docs/SCREENS.md](docs/SCREENS.md) | UI contract |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`)
- Git
- Optional: Docker with Compose, for the container setup, smtp4dev and a local Nextcloud

## Run from scratch

```bash
git clone https://github.com/atefekalamati/abrit-customer-panel.git
cd abrit-customer-panel
dotnet run --project src/AbrIT.Panel.Web        # Mock mode, http://localhost:5080
```

There is no setup screen. On the first run Orchard AutoSetup creates the site from the `AbrITPanel` recipe (services,
plans, roles) and the demo accounts, in a SQLite database under `src/AbrIT.Panel.Web/App_Data` (or
`$ORCHARD_APP_DATA`). Later runs reuse it. The port is fixed to 5080; if it is taken, the app stops with
"address already in use".

### Demo accounts

| Account | Password | Role |
|---|---|---|
| `admin` | `Local-Adm1n!pw` | Administrator: confirm test payments, retry, Drive connection page (`/Admin`) |
| `customer-a` | `Local-CustA1!` | customer |
| `customer-b` | `Local-CustB1!` | customer |

These are throwaway credentials from `appsettings.Local.json`, read only by the local launch profile
([D-28](docs/DECISIONS.md)). Docker and production take credentials from `.env` / environment variables. To start
over, delete `src/AbrIT.Panel.Web/App_Data`.

### Environments

`dotnet run` gets `ASPNETCORE_ENVIRONMENT=Local` from `Properties/launchSettings.json` and does **not** read `.env`.
Docker Compose reads `.env`; `.env.example` also says `Local`, and Compose uses `Production` if the value is unset.
`appsettings.Local.json` is not part of the published build, so a container never loads it. Do not use `Development`:
order pages then fail with HTTP 500 ([D-28](docs/DECISIONS.md)).

### Try the main scenario

1. Sign in as `customer-a`, open «سرویس‌ها» → AbrIT Drive → choose a plan → confirm the order.
2. Sign in as `admin` (another browser or a private window), open `/Admin/AbrIT/Orders`, confirm the test payment.
3. Back as `customer-a`, the order shows «فعال (آزمایشی – Mock)» with the safe access information.
4. To see failure and retry: on «تنظیمات اتصال Drive» make the next connector call fail, approve another order, then
   use «تلاش مجدد».

### Docker

```bash
cp .env.example .env       # then fill in ORCHARD_ADMIN_PASSWORD and, optionally, the demo customer passwords
docker compose up --build  # app http://localhost:8080, smtp4dev UI http://localhost:5081
```

Passwords need at least 8 characters with an upper-case letter, a lower-case letter, a digit and a symbol.

### Password-reset e-mail

With Docker, e-mail goes to **smtp4dev** (http://localhost:5081). With plain `dotnet run`, every e-mail is written as
an `.eml` file to `App_Data/mail/`. To check a reset: open `/ForgotPassword`, enter the user name, open the newest
`.eml`, and follow the `/ResetPassword?...` link (valid for 15 minutes).

## Tests

```bash
dotnet test                                      # 173 tests: Domain 115, Nextcloud 39, Web 19
dotnet test --filter Category=Integration        # acceptance flows against the real host on a temporary App_Data
dotnet format --verify-no-changes
```

CI (`.github/workflows/ci.yml`) runs format, build, tests and a Docker smoke check. `cd.yml` publishes a container
image to GitHub Container Registry after CI succeeds on `main` and on `v*` tags. Nothing is deployed anywhere.

## Mock and Real mode

| | Mock (default) | Real |
|---|---|---|
| What it does | In-memory fake accounts; no network, no keys | Calls the Nextcloud **OCS Provisioning API** |
| What the UI says | «فعال (آزمایشی – Mock)», «حالت اتصال: Mock», login button disabled | «فعال», «حالت اتصال: Real», Drive login link |
| How it was tested | Unit tests, integration tests, browser runs | **Only unit tests against a fake HTTP handler** |

> **Real mode has not been run against a live Nextcloud server**, neither one provided by AbrIT nor a local one
> ([D-31](docs/DECISIONS.md)). Treat it as implemented to the API contract and unproven end to end.

Mock fake accounts live in memory, so they are gone after a restart. Orders and their statuses are persisted.

| Mock option | Default | Effect |
|---|---|---|
| `Nextcloud__Mock__FailNextCalls` | `0` | The next N connector calls fail (AC-06); also adjustable on the admin «تنظیمات اتصال Drive» page |
| `Nextcloud__Mock__DelayMs` | `0` | Development only: wait before creating the account, to show «در حال فعال‌سازی» |

### Configuring Real mode (optional, unverified)

Point Real mode only at a Nextcloud **provided by AbrIT** or at a local one you run yourself.

1. For a local server: set `LOCAL_NEXTCLOUD_ADMIN_PASSWORD` in `.env`, run
   `docker compose --profile nextcloud up -d nextcloud`, and wait for http://localhost:8081.
2. In Nextcloud create an **app password** (*Personal settings → Security → Devices & sessions*).
3. Set these as environment variables or `dotnet user-secrets`, never in `appsettings*.json`:

   | Key | Value |
   |---|---|
   | `Nextcloud__Mode` | `Real` |
   | `Nextcloud__BaseUrl` | `http://localhost:8081` (`dotnet run`) or `http://nextcloud` (inside Compose) |
   | `Nextcloud__AdminUser` | the Nextcloud admin user |
   | `Nextcloud__AppPassword` | the app password |
   | `Nextcloud__TimeoutSeconds` | optional, default `15` |

   With Docker Compose, set the `NEXTCLOUD_*` variables in `.env`. If Mode is `Real` and a key is missing, the app
   refuses to start and names the missing keys, never their values.

The panel never sees, stores or shows a customer's Nextcloud password. Connector errors are stored as safe codes only
(`NC_AUTH`, `NC_TIMEOUT`, `NC_HTTP_{status}`, `NC_OCS_{code}`, `NC_NETWORK`, `NC_BAD_RESPONSE`).

### Simulated payment callback

`POST /api/payments/mock-callback` with body `{"orderId":"..."}` and header `X-Signature` (hex HMAC-SHA256 of the raw
body with `MockPayment__WebhookSecret`). It is disabled when the secret is empty and goes through the same idempotent
approval as the admin button.

## Project structure

```
src/
  AbrIT.Panel.Web/         Orchard Core host: Program.cs, configuration, /health/live
  AbrIT.Panel.Module/      Orchard module: controllers, views, permissions, YesSql persistence, recipe
  AbrIT.Panel.Domain/      Pure C#: order state machine, services, abstractions (no Orchard references)
  AbrIT.Panel.Nextcloud/   Mock and OCS connectors
  AbrIT.Theme/             RTL theme: layout, CSS, Vazirmatn font
design-system/             Design tokens (linked into the theme)
tests/                     Domain, Nextcloud and Web (integration) tests
docs/                      Documentation, screenshots, evidence
```

## Managing services and plans

Services and plans are Orchard content types (`AbrService`, `AbrPlan`, [D-02](docs/DECISIONS.md)), edited in
**Admin → Content → Content Items**. There is no custom catalog screen.

### How to add a new service

**Information-only or "coming soon" service (no code):** create an `AbrService` item with a `ServiceKey`, set
`Availability` to `InfoOnly` or `ComingSoon`, add `MoreInfoUrl` for a link, set `SortOrder`, and publish. It appears
on `/services`. An unknown key uses the generic support illustration. To seed it on new sites, add it to
`src/AbrIT.Panel.Module/Recipes/abrit-panel.recipe.json`.

**Orderable service (needs code).** Today only Drive can be ordered. Setting `Availability=Orderable` on another
service is not enough, because these parts are specific to Drive:

1. `Views/Services/Index.cshtml` shows the order button only when `ServiceKey == "drive"`.
2. `/services/drive` and `ServicesController.Drive` are the only plan page.
3. `Labels.ServiceName` derives the service name from a `drive-` plan key.
4. Provisioning calls one connector, `INextcloudProvisioner`, with quota and group concepts.

The steps would be: generalise the plan page to `/services/{serviceKey}`; define a provisioner interface for the new
service (or a common one); register implementations by `ServiceKey` and let `ProvisioningService` choose one; add the
`AbrService` and `AbrPlan` content; cover it with domain tests. The order state machine, payment approval, ownership
checks and retry rules do not depend on the service.

## Login link from abrit.ir: technical proposal

**Not implemented. The main site was not touched.**

- **Step 1, a plain link.** abrit.ir gets a «ورود به پنل» link to `https://panel.abrit.ir/Login`. No data is passed
  and no trust is needed between the sites.
- **Step 2, single sign-on.** If abrit.ir (or an AbrIT identity service) can act as an OpenID Connect provider, the
  panel signs users in with Orchard's OpenID Connect client: authorization code flow with PKCE, exact redirect URIs,
  client secret held in server configuration.
- **Account linking.** Match on the provider's stable subject id. Never link on an unverified e-mail address, because
  that allows account takeover.
- **Never** pass credentials or long-lived tokens in a URL, and do not share cookies or a user database between sites.
- Roll out behind a switch, keep local sign-in for administrators, and test sign-out and disabled accounts first.

## Assumptions

All assumptions made where the task was ambiguous are recorded in [docs/DECISIONS.md](docs/DECISIONS.md).
Known limitations are listed in [docs/REPORT.md](docs/REPORT.md#known-limitations).
