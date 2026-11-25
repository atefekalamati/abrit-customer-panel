/* AbrIT theme: minimal vanilla JS for the mobile drawer, password show/hide and live password rules. No framework. */
(function () {
  'use strict';

  // ---- Mobile drawer (<=640px): aria-expanded, Esc, focus trap, focus return ----
  var side = document.getElementById('abr-drawer');
  var burger = document.querySelector('[data-abr-drawer-open]');
  var backdrop = document.querySelector('.abr-backdrop');
  if (side && burger && backdrop) {
    var lastFocus = null;
    var isOpen = function () { return side.classList.contains('is-open'); };
    var focusables = function () { return side.querySelectorAll('a[href], button:not([disabled])'); };
    var phone = window.matchMedia('(max-width: 640px)');
    // Closed drawer on phones: off-canvas, hidden and inert (no focus, no clicks, not announced).
    var syncInert = function () { side.inert = phone.matches && !isOpen(); };
    var animate = function () {
      side.classList.add('abr-side--anim');
      window.setTimeout(function () { side.classList.remove('abr-side--anim'); }, 600);
    };
    syncInert();
    var open = function () {
      lastFocus = document.activeElement;
      animate();
      side.classList.add('is-open');
      syncInert();
      backdrop.hidden = false;
      burger.setAttribute('aria-expanded', 'true');
      document.body.classList.add('abr-noscroll');
      var first = focusables()[0];
      if (first) { window.setTimeout(function () { first.focus(); }, 30); }
    };
    var close = function () {
      if (!isOpen()) { return; }
      animate();
      side.classList.remove('is-open');
      syncInert();
      backdrop.hidden = true;
      burger.setAttribute('aria-expanded', 'false');
      document.body.classList.remove('abr-noscroll');
      (lastFocus || burger).focus();
    };
    burger.addEventListener('click', function () { if (isOpen()) { close(); } else { open(); } });
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
    var desktop = window.matchMedia('(min-width: 641px)');
    if (desktop.addEventListener) { desktop.addEventListener('change', function (m) { if (m.matches) { close(); } syncInert(); }); }
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
