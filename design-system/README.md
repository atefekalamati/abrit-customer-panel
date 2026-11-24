# AbrIT Panel — Design System v1.0 (RTL · فارسی)

Files:
- `tokens.css` is the **single source of truth**. The theme, `docs/prototype/index.html` and `landing/` import it.
- `tokens.json` is generated (`python3 design-system/build-tokens.py`) and is for Figma (Tokens Studio) or other tools.
- `fonts/Vazirmatn-Variable.woff2` + `OFL.txt` is the self-hosted Vazirmatn v33.0.3 font (SIL OFL, free to embed). No Google Fonts or CDN.

Copy into the Orchard theme as `src/AbrIT.Theme/wwwroot/css/tokens.css` + `wwwroot/fonts/`, and load it **before** `theme.css`.

---

## 1. Principles
1. **Clarity over decoration.** This is an operations panel, so status and next action come first.
2. **Honest states.** Mock, simulated payment and failures are always labelled. Nothing pretends to be real.
3. **RTL first.** Layout is designed right-to-left. Latin fragments are isolated, never the other way round.
4. **Accessible by default.** WCAG 2.2 AA is the floor, not a goal.

## 2. Colour

| Token | Hex | Use | Contrast (measured) |
|---|---|---|---|
| `--abr-primary` | `#0761BD` | buttons, links, focus, active nav | 6.08 on white · 5.53 on bg · white text on it 6.08 |
| `--abr-primary-hover` | `#033160` | hover/pressed | — |
| `--abr-primary-soft` | `#F2F8FD` | selected rows, info bg, totals | primary on it 5.68 |
| `--abr-nav-bg` | `#0F1B2A` | top navigation | muted text 9.25 |
| `--abr-bg` | `#F4F4F4` | page background | — |
| `--abr-surface` | `#FFFFFF` | cards, containers, inputs | — |
| `--abr-border` | `#E9EBED` | card outlines (**decorative only**) | 1.2 (never the only boundary of a control) |
| `--abr-border-strong` | `#8C8C94` | input and control borders | 3.34 ≥ 3:1 (WCAG 1.4.11) |
| `--abr-text` | `#000716` | body text | 20.1 |
| `--abr-text-2` | `#5F6B7A` | secondary text, labels | 5.43 white · 4.93 bg |

**Status colours** (foreground on its own background, all ≥ 4.5:1):

| Status | fg / bg | Ratio | Order states |
|---|---|---|---|
| success | `#037F0C` / `#EFFFF1` | 5.00 | Active (Real only) |
| info | `#0761BD` / `#F2F8FD` | 5.68 | Paid, Provisioning |
| warning | `#8D6605` / `#FFFCE6` | 5.04 | PendingPayment |
| error | `#D91515` / `#FFF7F7` | 4.89 | Failed |
| neutral | `#5F6B7A` / `#F4F4F4` | 4.93 | Cancelled, «به‌زودی» |
| mock | `#7D2105` / `#FEF6F0` | 9.40 | ActiveMock |

Rules:
- **Never colour alone** (WCAG 1.4.1). Every status has text, plus a dot or icon.
- Mock is its own colour, not green, so a fake activation can never be mistaken for a real one.
- The primary blue was darkened from Cloudscape's `#0972D3`. That blue measured 4.38:1 as link text on the gray page background, which fails AA.
- No violet or purple anywhere.

## 3. Typography — Vazirmatn

| Token | Size / line | Weight | Use |
|---|---|---|---|
| display | 32 / 44 | 700 | prices, stat numbers |
| h1 | 34 / 50 (mobile 24 / 36) | 800 | page title, one per page |
| card-title | 26 / 40 | 800 | auth / hero card titles |
| h2 | 20 / 32 | 700 | section, plan name |
| h3 | 18 / 28 | 700 | container title |
| body | **15 / 26** | 400 | default text |
| sm | 13 / 22 | 400–600 | labels, table header, notes |
| xs | 12 / 20 | 600 | badges only. **Minimum size**, nothing smaller |

Persian rules:
- Line height is about 1.7× for body text, because Persian has tall ascenders and dots.
- **No letter-spacing** (it breaks joined letters). No italics or uppercase.
- Body text is 15px (not 14), because Persian glyphs look smaller than Latin at the same size.
- Headings stay one level at a time (h1 → h2 → h3). One h1 per page.
- Persian digits (۰–۹) come from `Intl.NumberFormat('fa-IR')`, or `CultureInfo("fa-IR")` in C#. Thousands separator is «٬». Money is «۹۹٬۰۰۰ تومان».
- Dates use the Jalali calendar (`PersianCalendar`), written as `۱۴۰۵/۰۹/۰۱`. Time is `۱۴:۰۶`.
- **Codes stay Latin and LTR**: order numbers, emails, usernames, URLs, `(Mock)`, plan names. Wrap them in `<bdi>` or `dir="ltr"`, and use mono for long ids if needed.

## 4. Spacing & layout
- 4-pt grid: `4 · 8 · 12 · 16 · 20 · 24 · 32 · 40 · 48 · 64 · 80`.
- Inside a card the gap is 16. Between cards it is 24. Between page sections it is 24–32.
- Page padding is 80px on desktop, 32px at 1024px and below, and 16px at 640px and below. Content is at most 1280px wide.
- **Breakpoints:** `≤640` phone (one column, tables become cards, full-width buttons, burger menu), `≤1024` tablet (2 columns), `>1024` desktop.
- Touch targets are **≥ 44×44px** for every button, link outside running text, chip and checkbox (WCAG 2.5.8 sets 24px as the minimum; we use 44).

## 5. Radius, elevation, motion

| Token | Value | Use |
|---|---|---|
| radius-xs | 4 | badges |
| radius-sm | 8 | inputs |
| radius-md | 12 | tiles, alerts |
| radius-lg | 16 | cards, containers |
| radius-pill | 999 | buttons, chips |
| shadow-1 | `0 1px 4px` 8% | cards |
| shadow-2 | `0 4px 16px` 12% | hover lift, menus |
| shadow-3 | `0 12px 32px` 18% | dialogs |

Motion:
- Durations are 120 / 200 / 320 ms with ease `cubic-bezier(.2,0,0,1)`.
- **Under `prefers-reduced-motion` all durations become 0.**
- Hover lifts a card by 2px. Press scales to 0.98.
- No animation on scroll in the panel.

## 6. Components (state contract)

Every interactive component has these states: **default · hover · focus-visible · active/pressed · disabled · loading**, plus error where the component takes input.

| Component | Key rules |
|---|---|
| Button | One Primary per view. Label is a verb + object («ثبت سفارش», «تأیید پرداخت»). Min height 44. Loading state: `aria-busy="true"` + disabled + spinner, keeping its width. Destructive actions (cancel order) ask for confirmation. |
| Link | Underlined on hover **and** focus. External links say so («در abrit.ir»), open in the same tab unless they leave the panel. |
| Input / field | Visible `<label>` above the field, never a placeholder alone. Error text goes under the field, linked by `aria-describedby`, with `aria-invalid="true"`. Inputs for emails and usernames use `dir="ltr"`. |
| StatusBadge | Text + dot. Its text must still make sense out of context, because screen readers read it in table cells. |
| Alert | Error uses `role="alert"`. Success and info use `role="status"`. Title + one sentence + next step. |
| Table | Real `<table>` with `<caption>` (visually hidden is fine) and `<th scope="col">`. Below 640px each row becomes a card, with labels from `data-label`. An empty state has a message and an action. |
| Stat tile | The number has a text label. A tile that filters the table is a button with `aria-pressed`. |
| Nav | Has a `<nav aria-label>`. The current page has `aria-current="page"`. The burger has `aria-expanded` + `aria-controls`. A skip link «پرش به محتوای اصلی» comes first. |
| Dialog (confirm) | `<dialog>` with focus trapped, Esc closes it, and focus returns to the trigger. |

## 7. Accessibility checklist (WCAG 2.2 AA)
- [ ] `lang="fa" dir="rtl"`, a unique `<title>` per page, one `<h1>`.
- [ ] Skip link, landmarks (`header`, `nav`, `main`, `footer`), logical tab order (right→left, top→bottom).
- [ ] Focus ring is always visible (`--abr-focus-ring`) and never hidden under the sticky nav (`scroll-padding-top`).
- [ ] Contrast: text ≥ 4.5 (large text ≥ 3), controls and focus ≥ 3. Use only tokens.
- [ ] Status is never conveyed by colour alone. Live updates go through `role="status"` or `aria-live="polite"`.
- [ ] Every form control has a label. Errors are announced and linked to their field.
- [ ] Targets ≥ 44px. Nothing scrolls sideways at 320px. Zoom to 200% works.
- [ ] `prefers-reduced-motion` is respected.

## 8. Voice (UI copy)
- Persian, polite «شما», short and concrete: «سفارش ثبت شد» and not «عملیات با موفقیت انجام گردید».
- Errors say what happened and what to do next, with no codes or stack traces. Never show secrets.
- Every simulated thing says so: «آزمایشی»، «شبیه‌سازی‌شده»، «Mock».
