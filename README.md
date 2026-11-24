# AbrIT Customer Panel (Orchard Core sample)

Sample customer panel for AbrIT on **Orchard Core 3.0.1** / .NET 10 with an isolated Nextcloud connector
(**Mock** by default, **Real** = OCS Provisioning API). It never touches abrit.ir, WHMCS or any production system.
The full README (screenshots, architecture, limitations) arrives with the delivery phase; assumptions are in
[docs/DECISIONS.md](docs/DECISIONS.md).

## Run

```bash
dotnet build && dotnet test
dotnet run --project src/AbrIT.Panel.Web        # Mock mode, http://localhost:5000
docker compose up --build                        # app :8080, smtp4dev UI :5080 (copy .env.example to .env first)
```

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
