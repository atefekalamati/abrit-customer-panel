# Delivery report

## Summary

The main scenario works end to end in **Mock** mode: register or sign in, order a Drive plan, confirm a simulated
payment as admin, provision a fake account with its quota, and see the result in the customer's panel, including
failure and a controlled retry. **Real** Nextcloud mode is written but has **never been run against a live server**.

## Implemented

- Accounts: registration, sign-in, sign-out, password reset, password change (Orchard Users).
- Catalog as Orchard content types, seeded by recipe: Drive (Starter, Team), ERP360, AbrIT Desk, IT support.
- Orders with a server-side state machine, price and quota snapshot, owner cancellation.
- Admin: order list and filters, simulated payment approval, cancel, retry, connection settings, failure simulation.
- Signed simulated payment callback.
- Provisioning with three layers of duplicate protection ([architecture](ARCHITECTURE.md)).
- Mock connector; OCS connector (see below).
- Persian right-to-left UI with Jalali dates, for desktop and mobile widths.
- Bonus: Team workspace (add and remove members up to the seat limit) in Mock; admin "read status" action.
- Dockerfile, Compose, CI with a Docker smoke check, image publishing to GHCR.

## What is mocked or simulated

| Item | Reality |
|---|---|
| Payment | Simulated. No gateway, no money. |
| Drive account (default) | Mock: an in-memory fake that is lost on restart. The UI says so. |
| Real Nextcloud mode | **Interface level only.** The OCS client is tested against a fake `HttpMessageHandler`. We had no AbrIT test instance, and no run against a local server was completed or recorded. |
| E-mail | smtp4dev with Docker, `.eml` files with `dotnet run`. |

**Correction:** [A-18](DECISIONS.md) and an earlier README said Real mode had been verified against a local Nextcloud.
There is no evidence of such a run; the pull request that added the connector says it had not been done.
[D-31](DECISIONS.md) replaces that statement.

## Test results

`dotnet format --verify-no-changes` is clean. `dotnet test` on the whole solution: **173 passed, 0 failed**
(Domain 115, Nextcloud 39, Web 19).

## Evidence for AC-02 to AC-06

The evidence is provided as a **set of screenshots**. The task allows either a short video or a set of screenshots; we
chose screenshots.

They were captured in a browser on a freshly set-up Mock host running the final UI. HTTP answers and fake-account
counters are in [evidence/run-log.txt](evidence/run-log.txt). Times in the screenshots come from the machine's clock.

| AC | Scenario | Evidence | Automated test |
|---|---|---|---|
| AC-02 | Customer orders Starter; it is pending payment | [order page](evidence/ac02-order-pending-payment.png) | `AC02_CustomerA_OrdersStarter_IsPendingPayment_WithOwnerAndAmount` |
| AC-03 | Admin confirms payment; order becomes Active | [pending list](evidence/ac03-admin-pending.png), [confirm dialog](evidence/ac03-admin-approve-dialog.png), [approved](evidence/ac03-admin-approved.png) | `AC03_AC04_AdminApproves_BecomesActiveMock_With1GiBQuota` |
| AC-04 | Mock creates one fake account with 1 GB | [customer view](evidence/ac04-customer-active-mock.png), [request with quota](evidence/ac04-admin-provisioning-detail.png), [account count 1](evidence/ac04-mock-account-count.png) | same test |
| AC-05 | Approval replayed twice and callback sent twice: still one account, one attempt | [count still 1](evidence/ac05-mock-account-count-after-repeats.png), [order detail](evidence/ac05-admin-after-repeats.png), [run log](evidence/run-log.txt) | `AC05_DoubleApproveAndCallback_CreatesOneExternalAccount`, `AC05_DuplicateRetry_CreatesNoSecondAccount` |
| AC-06 | Injected error gives Failed; admin retry gives Active | [failure switch](evidence/ac06-1-failure-toggle.png), [customer: failed](evidence/ac06-2-customer-failed.png), [admin: error and retry](evidence/ac06-3-admin-failed-retry.png), [after retry](evidence/ac06-4-admin-after-retry.png), [customer: active](evidence/ac06-5-customer-active-after-retry.png), [account count 2 for two orders](evidence/ac06-6-mock-account-count.png) | `AC06_ProvisioningFailure_SetsFailed_RetryActivates` |

### Other acceptance criteria

| AC | Proof |
|---|---|
| AC-01 registration, sign-in, reset | `AC01_*` tests; sign-in screens in [screenshots/](screenshots/) (`e1-*`) |
| AC-07 customer B cannot see A's order | `AC07_*` tests |
| AC-08 non-admin cannot approve | `AC08_*` tests |
| AC-09 data survives a restart | `AC09_RestartHost_SameAppData_OrderAndStatusPersist` |
| AC-10 mobile and desktop, right-to-left | Manual. Final UI, desktop and 375 px: anonymous catalog ([desktop](screenshots/review/final-services-anonymous-desktop.png), [mobile](screenshots/review/final-services-anonymous-mobile.png)), dashboard ([empty](screenshots/review/final-dashboard-empty-desktop.png), [desktop](screenshots/review/final-dashboard-desktop.png), [mobile](screenshots/review/final-dashboard-mobile.png)), [mobile menu](screenshots/review/final-sidebar-drawer-mobile.png), Team order ([desktop](screenshots/review/final-order-detail-desktop.png), [mobile](screenshots/review/final-order-detail-mobile.png)). Older captures directly in [screenshots/](screenshots/README.md) are development history. |

## Interface notes

- Signed-in pages have a navy sidebar with four items and a support card; its colours follow the design reference
  ([D-32](DECISIONS.md)). Anonymous pages have no sidebar.
- White text on the active menu item has a contrast of about 3.2:1 at the light end of its gradient and 4.9:1 at the
  dark end, so it is partly below 4.5:1.
- Repeated Mock/test wording was removed. The order status, the Mock banner, the connection mode and the simulated
  payment status still say it ([D-30](DECISIONS.md)).

## Security scan of the git history

On 2026-11-26 the whole history was scanned from a fresh mirror clone of the GitHub repository (all branches, tags and
pull-request refs) with **gitleaks 8.30.1** and **trufflehog 3.100.1** (verification turned off, so nothing found was
sent to an outside service). As a check that the tools work, both detected a generated test key in a throwaway
repository.

- **No real secret was found.**
- gitleaks reported nothing.
- trufflehog reported 3 matches, all false positives: made-up `user:password@cloud.example` addresses in two test
  files (`RequiredScenarioTests.cs`, `SecurityHelpersTests.cs`) used to test that passwords do not leak into error
  messages.
- Neither tool flagged the throwaway demo passwords in `appsettings.Local.json`. They **are** in git, on purpose (see
  Known limitations, [D-28](DECISIONS.md)).

## Key decisions

- Catalog as content types; orders and provisioning as YesSql documents ([D-02](DECISIONS.md), [D-03](DECISIONS.md)).
- Rules in a Domain project without Orchard references ([D-05](DECISIONS.md)).
- No Orchard Commerce: heavier than this one-plan flow and it would hide the logic being evaluated ([D-04](DECISIONS.md)).
- Provisioning is synchronous ([A-01](DECISIONS.md)); retry is admin-only with a limit of 5 ([A-02](DECISIONS.md)).
- Another customer's order returns 404 ([A-10](DECISIONS.md)).
- Mock is never shown as a real activation; repeated copies of the label were removed, the necessary ones kept
  ([D-30](DECISIONS.md)).

## A bug found late

Order pages reloaded every 5 seconds in **every** state, which cleared the "new member e-mail" field while a person
was typing. The view rendered an empty `data-abr-refresh` attribute when nothing was provisioning, and the script read
empty as "use 5 seconds". Fast automated form filling finished before the reload, so tests did not see it. Now only a
positive value starts the reload, and it waits while a form is in use. Checked in a browser: 30 characters typed
200 ms apart with a 6-second pause, no reload, member added; during provisioning the page still refreshed and reached
Active. A regression test checks that an Active page does not enable the reload. Screenshots:
[half-typed after the pause](screenshots/review/final-member-email-half-typed.png),
[member added](screenshots/review/final-member-added.png).

## Known limitations

- Real Nextcloud mode is unverified against a live server.
- If the process stops during a connector call, the order stays `Provisioning`; there is no automatic recovery and
  retry is offered only for `Failed`.
- Only Drive can be ordered; another orderable service needs code
  ([details](../README.md#how-to-add-a-new-service)).
- No real payment, invoice, renewal, expiry or account deletion. A removed team member's account is disabled, not
  deleted.
- The order snapshot has no billing period; the page shows «ماهانه», which is true for the seeded plans.
- The UI is Persian only. Orchard's own admin shell is largely English, and at phone width its menu opens over the
  content.
- Mock accounts and counters reset when the app restarts.
- `appsettings.Local.json` holds **throwaway demo passwords in git** so that a fresh clone runs with one command
  ([D-28](DECISIONS.md)). They are read only in the `Local` environment and must never be used elsewhere. This is a
  deliberate exception to "no secrets in git".
- SQLite and single-instance operation; no rate limiting or monitoring was added.
- `.env.example` sets `ASPNETCORE_ENVIRONMENT=Local` for Docker Compose. That value was checked on the published
  build with environment variables, **not inside a container**; the CI Docker smoke check runs with the default,
  `Production`.

## Not delivered

- Login from abrit.ir (proposal only, in the README).
- Separate GitHub Issues per assumption (they are recorded in [DECISIONS.md](DECISIONS.md)).

## Next steps

See [FUTURE.md](FUTURE.md).
