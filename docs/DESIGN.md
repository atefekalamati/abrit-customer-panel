# DESIGN.md — UI contract (Cloudscape-inspired, RTL)

Sources, in priority order:
0. **`design-system/`** — tokens (`tokens.css`, single source), font, rules, a11y checklist. Wins over everything below.
1. **`docs/prototype/index.html`** — every screen, desktop + mobile (toggle in the toolbar). The CSS between
   `THEME START` / `THEME END` **is** the theme stylesheet: copy it to `src/AbrIT.Theme/wwwroot/css/theme.css`
   and replace `@container viewport (max-width: 640px)` with `@media (max-width: 640px)`.
2. Figma (tokens + component library + D1, D2, D3, D7): https://www.figma.com/design/6NOl5F87wIROKD8fvKDjTB
   Variable names in Figma = CSS variable names below.
3. Superseded (old blue palette, do not use for colors): https://www.figma.com/design/h9nez4g71jjT3oFeHST4f0

Agents: read the prototype by section (`grep -n 'id="d4"'`), never the whole file at once.

## Tokens (defined in `design-system/tokens.css` — summary only)

| Group | Values |
|---|---|
| Brand | `--abr-primary #0761BD` (darkened from #0972D3 for AA contrast) · `--abr-primary-hover #033160` · `--abr-primary-soft #F2F8FD` |
| Nav | `--abr-nav-bg #FFF` · `--abr-nav-text #000716` · `--abr-nav-muted #5F6B7A` · active `--abr-nav-active-text #0761BD` on `--abr-nav-active-bg #F2F8FD` (5.68:1) |
| Surfaces | `--abr-bg #F4F4F4` · `--abr-surface #FFF` · `--abr-subtle #F9F9FA` · `--abr-border #E9EBED` · `--abr-border-strong #8C8C94` |
| Text | `--abr-text #000716` · `--abr-text-2 #5F6B7A` · `--abr-link #0972D3` |
| Status fg / bg | success `#037F0C/#EFFFF1` · error `#D91515/#FFF7F7` · warning `#8D6605/#FFFCE6` · info `#0972D3/#F2F8FD` · neutral `#5F6B7A/#F4F4F4` · mock `#7D2105/#FEF6F0` |
| Radius | container 16 · button 20 (pill) · tile 12 · input 8 · badge 4 |
| Spacing | 4 / 8 / 16 / 20 / 24 / 32 |
| Font | Vazirmatn variable, `design-system/fonts/` (self-hosted, OFL) · body 15/26 |

> abrit.ir's exact brand colors could not be read automatically. To match them, change only the `--abr-primary*` and `--abr-nav-bg` values.

Bootstrap 5 RTL mapping: `--bs-primary: var(--abr-primary)`, `--bs-body-bg: var(--abr-bg)`,
`--bs-body-font-family: var(--abr-font)`, `--bs-body-color: var(--abr-text)`. Our `.abr-*` classes win over Bootstrap.

## Typography

| Use | Size / line-height | Weight |
|---|---|---|
| Page title (`.abr-h1`) | 28/40 (mobile 22/34) | 700 |
| Container title | 18 | 700 |
| Plan price / stat | 32/44 | 700 |
| Body | 14/1.6 | 400 |
| Labels (`dt`), notes | 12–13 | 500 / 400 |
| Badge | 12 | 600 |

## Components → Razor partials

| Component (Figma / CSS) | Partial | Notes |
|---|---|---|
| TopNav `.abr-top` | theme `Layout.cshtml` | light sticky bar; sidebar toggle at its start (desktop: collapse, persisted; ≤1024px: off-canvas drawer) |
| Footer `.abr-footer` | `Layout.cshtml` | "محیط ارزیابی" note always visible |
| Button `.abr-btn--primary/normal/link/danger` | tag helper or plain class | one Primary per page |
| StatusBadge `.abr-badge[data-status]` | `_StatusBadge.cshtml(OrderStatus, ProvisionerMode)` | Active + Mock → `ActiveMock` «فعال (آزمایشی – <bdi>Mock</bdi>)»; `Active` only when Mode=Real succeeded |
| Container `.abr-container` | `_Container.cshtml` (title, desc, actions slot) | |
| KeyValue `.abr-kv-grid` / `.abr-kv` | `_KeyValueGrid.cshtml` | 3 cols desktop, 2 mobile |
| Alert `.abr-alert--info/error/mock/success` | `_Alert.cshtml` | error uses `role="alert"` |
| FormField `.abr-field` + `.abr-input` | restyle Orchard Users views | emails/usernames: `.ltr` |
| ServiceCard `.abr-service` | `_ServiceCard.cshtml` | driven by `Availability` (+ external link card) |
| PlanCard `.abr-plan` (`--recommended`) | `_PlanCard.cshtml` | Team = recommended |
| Table `.abr-table` | inline in views | `td[data-label]` required → becomes cards on mobile |
| Timeline `.abr-timeline` | `_StatusTimeline.cshtml` | from `Order.History` |
| Stat tile `.abr-stat`, chips `.abr-chip`, progress `.abr-progress` | inline | |

## Screens → routes

| Prototype id | Route | AC |
|---|---|---|
| d1 | `/services` | — |
| d2 | `/services/drive` | — |
| d3 | `/orders/new?plan={id}` (POST) | AC-02 |
| d4 | `/dashboard` | AC-02, AC-07 |
| d5 | `/dashboard/orders/{id}` — active (Mock) | AC-04 |
| d6 | `/dashboard/orders/{id}` — failed | AC-06 |
| d7 / d8 / d9 | Orchard `/Login`, `/Register`, `/ForgotPassword` (restyled) | AC-01 |
| a1 | `/Admin/AbrIT/Orders` (content area inside Orchard admin theme) | AC-03, AC-05, AC-06, AC-08 |
| a2 | `/Admin/AbrIT/Provisioning` | AC-06 demo |

Admin screens render inside Orchard's admin theme; use only the content-area markup + `.abr-*` classes
(load theme.css in admin via a resource manifest), don't restyle the whole admin.

## RTL & content rules
- `<html lang="fa" dir="rtl">`, `bootstrap.rtl.min.css`, logical properties (`margin-inline-start`, `padding-inline-start`).
- Latin tokens inside Persian text (order numbers, emails, usernames, URLs, `(Mock)`, plan names) → `<bdi>` / `.ltr`.
- Numbers in Persian digits with `fa-IR` grouping + «تومان»; dates Jalali (`PersianCalendar`).
- Never render a Nextcloud password or app password; access info = username, URL, quota, set-password note.
- AC-10 check: 375px and 1440px.
