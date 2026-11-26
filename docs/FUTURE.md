# With more time

None of this is implemented. Ordered by what matters most before real use.

## Prove and harden what exists

1. **Run Real mode against a live Nextcloud,** ideally a test instance provided by AbrIT: create, quota, group, team
   members, disable, status read, the welcome e-mail, wrong credentials and timeouts. Then record what differs from
   the fake-handler tests.
2. **Recover stuck orders.** If the process stops during a connector call, the order stays `Provisioning` and cannot
   be retried. Add a reconciliation job that checks the external account and moves stale requests to `Active` or
   `Failed`.
3. **Provision in the background.** Move the connector call out of the approval request into a durable queue with
   backoff, so a slow Nextcloud does not hold the admin's page.
4. **Browser tests in CI,** including slow, key-by-key typing. A page that reloaded every 5 seconds and wiped
   half-typed input passed all HTTP-level tests and fast form filling.
5. **Operations:** rate limits on sign-in, registration and the callback; e-mail confirmation; an audit log; metrics
   and alerts for failed provisioning; backups, and a server database instead of SQLite.

## Product

- **Real payment gateway:** signed callbacks with replay protection, reconciliation, invoices, refunds.
- **Subscription lifecycle:** renewal, expiry, suspension, upgrade or downgrade (quota change), and safe deletion with
  a retention period. Store the billing period in the order's plan snapshot.
- **More orderable services:** connectors chosen by `ServiceKey` (see
  [How to add a new service](../README.md#how-to-add-a-new-service)).
- **Customer notifications** for payment, activation and failure.
- **Translation** of the remaining English strings in Orchard's admin.
- **Contrast:** darken the light end of the sidebar's active-item gradient so its label reaches 4.5:1 everywhere.
- **Design consistency:** move raw font sizes and corner radii onto the tokens and align heading sizes between the
  customer and admin pages.

## Integration with AbrIT

- **Single sign-on** with abrit.ir through OpenID Connect
  (see the [proposal in the README](../README.md#login-link-from-abritir-technical-proposal)).
- **WHMCS migration,** if WHMCS remains the billing system for existing customers:
  1. Decide which system is authoritative for customers, invoices and services.
  2. Import read-only into a staging copy; keep the WHMCS id on every imported record.
  3. Link existing Drive accounts to orders **without** creating new ones. Imported services need their stored external
     id instead of the derived one.
  4. Reconcile in dry-run mode and review the differences before switching.
  5. Move over in small groups with a way back. Customers reset passwords rather than having password hashes copied.
