# Dashboard (D4 `/dashboard`) — UX & accessibility audit

Standard: WCAG 2.2 AA + `design-system/README.md` §6–7. Tools: axe-core 4 (wcag2a/aa, 2.1aa, 2.2aa, best-practice)
in headless Chrome at 1500px and 375px, a target-size script, and a manual keyboard and screen-reader pass.

| # | Issue (before) | WCAG / heuristic | Severity | Fix (after) |
|---|---|---|---|---|
| 1 | Link text `#0972D3` on the page background measured **4.38:1** | 1.4.3 Contrast | High | Primary/link darkened to `#0761BD` (5.53:1 on bg, 6.08 on white) in `tokens.css` |
| 2 | Actions column header was empty (axe `empty-table-header`) | 1.3.1 | Medium | `<th scope="col"><span class="sr-only">عملیات</span>` |
| 3 | Table had no caption and no `scope` on headers | 1.3.1 | Medium | `<caption class="sr-only">`, `scope="col"` on all headers |
| 4 | **12 controls under 44px** (nav links 22px, order links 16px, buttons 38px) | 2.5.8 Target size + touch UX | High | Buttons, nav links and logout are ≥ 44px. Order-number links use `.abr-cell-link` (44px). Breadcrumb is 26px (≥ 24 AA). |
| 5 | No skip link. Focus ring missing on links. Focus could hide under the nav | 2.4.1, 2.4.7, 2.4.11 | High | Skip link «پرش به محتوای اصلی» on every screen, global `:focus-visible` ring token, `scroll-padding-top` |
| 6 | Burger button had no state, and the menu never opened | 4.1.2 | High (mobile) | `aria-expanded` + `aria-controls`, working menu |
| 7 | «خروج» was a GET link (Orchard needs POST, and a link can be prefetched) | Security / 4.1.2 | Medium | `<form method="post">` + button |
| 8 | «لغو سفارش» was destructive with one click and no confirmation | Nielsen #5 error prevention | High | Red outlined button → `<dialog>` with focus on «انصراف», Esc closes, focus returns to the trigger |
| 9 | Repeated «مشاهده جزئیات» links were ambiguous in screen-reader link lists | 2.4.4 / 2.4.9 | Medium | `aria-label="مشاهده جزئیات سفارش ABR-…"` |
| 10 | Stat tiles showed meaning by colour only (red/green numbers with inline styles) and did nothing when clicked | 1.4.1 + affordance | Medium | Dot + text label + neutral number. Tiles are now **filter buttons** with `aria-pressed`. The result count is announced via `aria-live` |
| 11 | «نیاز به پیگیری» told the customer nothing actionable | Nielsen #2 (match real world) | Medium | Renamed «خطا در فعال‌سازی». The row gets the hint «پشتیبانی در حال بررسی است؛ نیازی به ثبت دوباره نیست» |
| 12 | No feedback after placing an order (D3 → D4) | Nielsen #1 visibility | Medium | Success alert with `role="status"` naming the order and the next step |
| 13 | No empty state, so a filter could leave a blank card | Nielsen #1 | Low | «سفارشی با این وضعیت ندارید» + «نمایش همه‌ی سفارش‌ها» |
| 14 | Mixed Latin plan names broke cell layout on mobile («— AbrIT Drive Starter») | 1.3.2 meaningful sequence | Low | Plan names in `<bdi>`, cell content wrapped |
| 15 | Dates were plain text | 1.3.1 | Low | `<time datetime="2026-11-23">۱۴۰۵/۰۹/۰۲</time>` |
| 16 | Avatar letter was read aloud before the user name | 1.1.1 | Low | `aria-hidden="true"` |
| 17 | Body text 14px was small for Persian glyphs | readability | Low | 15/26 via `--abr-fs-body` |
| 18 | Breadcrumb was a plain div | 1.3.1 | Low | `<nav aria-label="مسیر صفحه">` + `aria-current="page"` |

**After:** axe reports 0 violations at 1500px and 375px. One target under 44px remains: the breadcrumb link (26px, which passes the 24px AA minimum).
Reduced motion: hover lift and press scale are off under `prefers-reduced-motion`.

## For implementation (phase 06)
- Filtering in the real app: either a query string (`/dashboard?status=failed`, which works without JS and is bookmarkable) or this client-side filter as progressive enhancement. **Prefer the query string.**
- The cancel dialog form POSTs to `/dashboard/orders/{id}/cancel` with antiforgery. The server rejects the request unless the order is owned by the user **and** still `PendingPayment`.
- The success alert comes from Orchard `INotifier` (TempData) after the PRG redirect from D3.
- The stat counts come from one grouped query on `OrderIndex` filtered by `OwnerUserId`.
