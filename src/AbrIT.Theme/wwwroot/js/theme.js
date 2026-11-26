/* AbrIT theme: minimal vanilla JS for the sidebar toggle/drawer, password show/hide and live password rules. No framework. */
(function () {
  'use strict';

  // ---- Sidebar toggle (top-bar button) ----
  // Desktop (>1024px): collapses/expands the sidebar column; the choice is persisted in localStorage.
  // Below that: off-canvas drawer (closed on every load) with aria-expanded, Esc, focus trap and focus return.
  // A hidden sidebar is always inert (no focus, no clicks, not announced).
  var side = document.getElementById('abr-drawer');
  var burger = document.querySelector('[data-abr-drawer-open]');
  var backdrop = document.querySelector('.abr-backdrop');
  if (side && burger && backdrop) {
    var root = document.documentElement;
    var KEY = 'abr-side';
    var lastFocus = null;
    var compact = window.matchMedia('(max-width: 1024px)');
    var isOpen = function () { return side.classList.contains('is-open'); };
    var isCollapsed = function () { return root.classList.contains('abr-side-collapsed'); };
    var focusables = function () { return side.querySelectorAll('a[href], button:not([disabled])'); };
    var save = function (value) { try { window.localStorage.setItem(KEY, value); } catch (e) { /* storage blocked: keep in-page state only */ } };
    var sync = function () {
      var shown = compact.matches ? isOpen() : !isCollapsed();
      side.inert = !shown;
      burger.setAttribute('aria-expanded', String(shown));
      burger.setAttribute('aria-label', shown ? 'بستن منو' : 'باز کردن منو');
    };
    var animate = function () {
      side.classList.add('abr-side--anim');
      window.setTimeout(function () { side.classList.remove('abr-side--anim'); }, 600);
    };
    var open = function () {
      lastFocus = document.activeElement;
      animate();
      side.classList.add('is-open');
      backdrop.hidden = false;
      document.body.classList.add('abr-noscroll');
      sync();
      var first = focusables()[0];
      if (first) { window.setTimeout(function () { first.focus(); }, 30); }
    };
    var close = function () {
      if (!isOpen()) { return; }
      animate();
      side.classList.remove('is-open');
      backdrop.hidden = true;
      document.body.classList.remove('abr-noscroll');
      sync();
      (lastFocus || burger).focus();
    };
    var toggleDesktop = function () {
      var collapse = !isCollapsed();
      root.classList.toggle('abr-side-collapsed', collapse);
      save(collapse ? 'closed' : 'open');
      sync();
    };
    sync();
    burger.addEventListener('click', function () {
      if (!compact.matches) { toggleDesktop(); } else if (isOpen()) { close(); } else { open(); }
    });
    document.querySelectorAll('[data-abr-drawer-close]').forEach(function (el) { el.addEventListener('click', close); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') { close(); } });
    side.addEventListener('keydown', function (e) {
      if (e.key !== 'Tab' || !isOpen()) { return; }
      var items = focusables();
      var first = items[0];
      var last = items[items.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    });
    var onBreakpoint = function () { if (!compact.matches) { close(); } sync(); };
    if (compact.addEventListener) { compact.addEventListener('change', onBreakpoint); }
  }

  // ---- Password show/hide ----
  document.querySelectorAll('[data-abr-pw-toggle]').forEach(function (btn) {
    var input = document.getElementById(btn.getAttribute('aria-controls'));
    if (!input) { return; }
    btn.addEventListener('click', function () {
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      btn.setAttribute('aria-pressed', String(show));
      btn.setAttribute('aria-label', show ? 'پنهان کردن رمز عبور' : 'نمایش رمز عبور');
    });
  });

  // ---- Live password rules (same rules as the server's IdentityOptions) ----
  var tests = {
    len: function (v, n) { return v.length >= n; },
    upper: function (v) { return /[A-Z]/.test(v); },
    lower: function (v) { return /[a-z]/.test(v); },
    digit: function (v) { return /[0-9]/.test(v); },
    symbol: function (v) { return /[^A-Za-z0-9]/.test(v); },
    unique: function (v, n) { return new Set(v.split('')).size >= n; }
  };
  document.querySelectorAll('[data-abr-rules]').forEach(function (list) {
    var input = document.getElementById(list.getAttribute('data-abr-rules'));
    if (!input) { return; }
    var check = function () {
      list.querySelectorAll('[data-rule]').forEach(function (li) {
        var test = tests[li.getAttribute('data-rule')];
        var ok = !!test && test(input.value, Number(li.getAttribute('data-n')) || 0);
        li.setAttribute('data-ok', String(ok));
        var state = li.querySelector('.sr-only');
        if (state) { state.textContent = ok ? '(رعایت شده)' : '(هنوز رعایت نشده)'; }
      });
    };
    input.addEventListener('input', check);
    check();
  });

  // ---- Single-submit forms (order placement): disable the button after the first submit ----
  document.querySelectorAll('form[data-abr-once]').forEach(function (form) {
    form.addEventListener('submit', function () {
      form.querySelectorAll('button[type=submit]').forEach(function (b) {
        window.setTimeout(function () { b.disabled = true; b.setAttribute('aria-busy', 'true'); }, 0);
      });
    });
  });
})();
