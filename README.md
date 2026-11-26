# AbrIT Customer Panel (Orchard Core sample)

Sample customer panel for AbrIT on **Orchard Core 3.0.1** / .NET 10 with an isolated Nextcloud connector
(**Mock** by default, **Real** = OCS Provisioning API). It never touches abrit.ir, WHMCS or any production system.
The full README (screenshots, architecture, limitations) arrives with the delivery phase; assumptions are in
[docs/DECISIONS.md](docs/DECISIONS.md).

## Run

From a fresh clone, at the repo root, one command (.NET 10 SDK):

```bash
dotnet run --project src/AbrIT.Panel.Web        # Mock mode, http://localhost:5080
```

The first run sets itself up with no Setup screen: Orchard AutoSetup creates the site from the `AbrITPanel` recipe
(services, plans, roles) and the demo accounts below, in a SQLite database under `src/AbrIT.Panel.Web/App_Data`
(or `$ORCHARD_APP_DATA`). Later runs reuse it. The port is fixed to 5080 by `Properties/launchSettings.json`; if
5080 is taken the app stops with "address already in use" instead of picking another port.

| Account | Password | Role |
|---|---|---|
| `admin` | `Local-Adm1n!pw` | Administrator (approve test payments, retry, Drive connection page) |
| `customer-a` | `Local-CustA1!` | customer |
| `customer-b` | `Local-CustB1!` | customer |

These are throwaway local demo credentials from `appsettings.Local.json` (the `Local` environment that the launch
profile sets; see
[D-28](docs/DECISIONS.md)). Docker and production never read them: they take credentials from `.env` / environment
variables. To start over, delete `src/AbrIT.Panel.Web/App_Data` (or point `ORCHARD_APP_DATA` at an empty folder).

```bash
dotnet build && dotnet test
docker compose up --build                        # app :8080, smtp4dev UI :5081 (copy .env.example to .env first)
dotnet test --filter Category=Integration        # AC-01..AC-09 against the real host (temp App_Data, ~20 s)
```

### E-mail (password reset) without Docker

With Docker, Orchard's SMTP provider sends to **smtp4dev** (open http://localhost:5081 to read the reset e-mail).
When no SMTP host is configured (`OrchardCore__OrchardCore_Email_Smtp__Host` empty, e.g. plain `dotnet run`), the app
uses SMTP **pickup-directory** delivery instead: every e-mail is written as an `.eml` file to `App_Data/mail/`
(or `$ORCHARD_APP_DATA/mail/`). To verify a password reset by hand: open `/ForgotPassword`, enter the user name, open
the newest `.eml` in `App_Data/mail/`, and follow the `/ResetPassword?...` link (valid 15 minutes).

### Mock connector options (dev / demo)

| Variable | Default | Effect |
|---|---|---|
| `Nextcloud__Mock__FailNextCalls` | `0` | The next N connector calls fail (AC-06 demo); also adjustable on the admin «تنظیمات اتصال Drive» page |
| `Nextcloud__Mock__DelayMs` | `0` | **Dev-only.** The Mock waits this many ms (max 60000) before creating the account, so the «در حال فعال‌سازی» state can be seen / captured. Never set in tests or CI |

## Real Nextcloud mode (optional)

Mock stays the default everywhere (demo, CI, video). Real mode is only ever pointed at a Nextcloud **provided by
AbrIT** or at the **local container below**. Our own verification used the local container — that is
*not an AbrIT environment* (DECISIONS A-18).

1. Start a local Nextcloud (needs Docker Hub access): set `LOCAL_NEXTCLOUD_ADMIN_PASSWORD` in `.env`, then
   `docker compose --profile nextcloud up -d nextcloud` and wait for http://localhost:8081 to show the login page
   (first start installs with SQLite and takes about a minute).
2. Create an **app password**: log in as `LOCAL_NEXTCLOUD_ADMIN_USER` → avatar → *Personal settings* → *Security* →
   *Devices & sessions* → name it `abrit-panel` → *Create new app password*. Copy it once; never commit it.
3. Configure the panel (env vars or `dotnet user-secrets`, never `appsettings*.json`):

   | Key | Value |
   |---|---|
   | `Nextcloud__Mode` | `Real` |
   | `Nextcloud__BaseUrl` | `http://localhost:8081` (`dotnet run`) or `http://nextcloud` (app inside compose) |
   | `Nextcloud__AdminUser` | the admin user name |
   | `Nextcloud__AppPassword` | the app password from step 2 |
   | `Nextcloud__TimeoutSeconds` | optional, default `15` |

   With `docker compose` set the `NEXTCLOUD_*` variables in `.env` instead. If Mode is `Real` and any key is missing,
   the app refuses to start and lists the missing keys (never their values).
4. Order a plan and approve the payment in the admin. The account `abrit-ord{year}-{seq}` is created with the
   customer's e-mail; Nextcloud sends the set-password e-mail (visible in smtp4dev). The panel never sees, stores or
   shows a Nextcloud password. Team orders also create the group `abrit-team-{year}-{seq}`; members added from the
   order page get their own accounts in that group.

Connector errors are stored as safe codes only (`NC_AUTH`, `NC_TIMEOUT`, `NC_HTTP_{status}`, `NC_OCS_{code}`,
`NC_NETWORK`, `NC_BAD_RESPONSE`) and the admin can use the controlled Retry.

## Managing services and plans

The catalog (services and their plans) lives in Orchard Core content types `AbrService` and `AbrPlan`
([D-02](docs/DECISIONS.md)), so it is edited in Orchard's own admin: **Admin → Content → Content Items**, filtered by
type. There is no custom catalog screen. The design image `16-admin-manage-content.png` (in the design references)
only illustrates what this built-in screen covers: listing, editing, publishing and ordering services and plans.

## Known limitations (so far)

- Plan snapshots on an order do not store the billing period; the order page shows «ماهانه», which is true for every
  seeded plan. A non-monthly plan would need the period added to the snapshot.
