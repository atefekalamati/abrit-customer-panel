/* AbrIT panel UI helpers shared by customer and admin pages (no framework):
   - form[data-abr-confirm]: accessible confirm <dialog> before submitting (never window.confirm);
     without JS the form simply submits (the server still validates everything).
   - [data-abr-copy=<id>]: copy an element's text, announce the result in a live region.
   - [data-abr-refresh=<seconds>]: reload while an order is being provisioned. */
(function () {
  'use strict';

  var live = document.createElement('div');
  live.style.cssText = 'position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap';
  live.setAttribute('role', 'status');
  live.setAttribute('aria-live', 'polite');
  document.body.appendChild(live);
  var announce = function (text) { live.textContent = ''; window.setTimeout(function () { live.textContent = text; }, 50); };

  // ---- Confirm dialog ----
  var icons = {
    card: '<rect x="3" y="5.5" width="18" height="13" rx="2"/><path d="M3 10h18M7 15h4"/>',
    close: '<path d="M6 6l12 12M18 6 6 18"/>',
    trash: '<path d="M4 7h16"/><path d="M9.5 7V4.5h5V7"/><path d="M6 7l1 13a1.5 1.5 0 0 0 1.5 1.4h7A1.5 1.5 0 0 0 17 20l1-13"/><path d="M10 11v6M14 11v6"/>',
    refresh: '<path d="M20 11a8 8 0 0 0-14.6-4.5L4 8"/><path d="M4 3.5V8h4.5"/><path d="M4 13a8 8 0 0 0 14.6 4.5L20 16"/><path d="M20 20.5V16h-4.5"/>',
    warning: '<path d="M12 3.5 2.5 20h19Z"/><path d="M12 10v4.5"/><path d="M12 17.4h.01"/>'
  };
  var svg = function (name, size) {
    return '<svg width="' + size + '" height="' + size + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">' + (icons[name] || icons.warning) + '</svg>';
  };
  var dialogs = {};
  var getDialog = function (host) {
    var key = host === document.body ? 'body' : 'admin';
    if (dialogs[key]) { return dialogs[key]; }
    var d = document.createElement('dialog');
    d.className = 'abr-dialog';
    d.setAttribute('aria-labelledby', 'abr-dialog-title-' + key);
    d.setAttribute('aria-describedby', 'abr-dialog-body-' + key);
    d.innerHTML =
      '<form method="dialog" class="abr-dialog__box">' +
      '<button type="submit" value="cancel" class="abr-dialog__x" aria-label="بستن">' + svg('close', 20) + '</button>' +
      '<span class="abr-dialog__icon"></span>' +
      '<h2 class="abr-dialog__title" id="abr-dialog-title-' + key + '"></h2>' +
      '<p class="abr-dialog__body" id="abr-dialog-body-' + key + '"></p>' +
      '<p class="abr-dialog__note" hidden></p>' +
      '<div class="abr-dialog__actions">' +
      '<button type="submit" value="ok" class="abr-btn abr-dialog__ok"></button>' +
      '<button type="submit" value="cancel" class="abr-btn abr-btn--normal abr-dialog__cancel">انصراف</button>' +
      '</div></form>';
    host.appendChild(d);
    dialogs[key] = d;
    return d;
  };

  document.querySelectorAll('form[data-abr-confirm]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      if (form.getAttribute('data-confirmed') === 'true') { return; }
      var host = form.closest('.abr-admin') || document.body;
      var d = getDialog(host);
      if (typeof d.showModal !== 'function') { return; } // very old browser: submit without the extra step
      e.preventDefault();
      var tone = form.getAttribute('data-confirm-tone') || 'primary';
      var note = form.getAttribute('data-confirm-note');
      d.setAttribute('data-tone', tone);
      d.querySelector('.abr-dialog__icon').innerHTML = svg(form.getAttribute('data-confirm-icon') || 'warning', 32);
      d.querySelector('.abr-dialog__title').textContent = form.getAttribute('data-confirm-title') || '';
      d.querySelector('.abr-dialog__body').textContent = form.getAttribute('data-confirm-body') || '';
      var noteEl = d.querySelector('.abr-dialog__note');
      noteEl.hidden = !note;
      noteEl.innerHTML = note ? svg('warning', 20) + '<span></span>' : '';
      if (note) { noteEl.querySelector('span').textContent = note; }
      var ok = d.querySelector('.abr-dialog__ok');
      ok.textContent = form.getAttribute('data-confirm-ok') || 'تأیید';
      ok.className = 'abr-btn abr-dialog__ok ' + (tone === 'danger' ? 'abr-btn--danger-solid' : 'abr-btn--primary');
      var trigger = e.submitter || form.querySelector('[type=submit]');
      d.returnValue = '';
      d.onclose = function () {
        if (d.returnValue === 'ok') {
          form.setAttribute('data-confirmed', 'true');
          form.querySelectorAll('button[type=submit]').forEach(function (b) { b.disabled = true; b.setAttribute('aria-busy', 'true'); });
          HTMLFormElement.prototype.submit.call(form);
        } else if (trigger) {
          trigger.focus();
        }
      };
      d.showModal();
      d.querySelector('.abr-dialog__cancel').focus(); // safest default: Enter does not confirm by accident
    });
  });

  // ---- Copy buttons ----
  document.querySelectorAll('[data-abr-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var src = document.getElementById(btn.getAttribute('data-abr-copy'));
      if (!src || !navigator.clipboard) { announce('کپی در این مرورگر پشتیبانی نمی‌شود'); return; }
      navigator.clipboard.writeText(src.textContent.trim()).then(function () {
        btn.setAttribute('data-copied', 'true');
        announce('کپی شد');
        window.setTimeout(function () { btn.removeAttribute('data-copied'); }, 1500);
      }, function () { announce('کپی انجام نشد'); });
    });
  });

  // ---- Auto refresh while provisioning (stops when the tab is hidden) ----
  var refresh = document.querySelector('[data-abr-refresh]');
  if (refresh) {
    var seconds = Math.max(3, Number(refresh.getAttribute('data-abr-refresh')) || 5);
    window.setTimeout(function tick() {
      if (document.hidden) { window.setTimeout(tick, seconds * 1000); return; }
      window.location.reload();
    }, seconds * 1000);
  }

  // ---- Expandable admin rows: rows stay visible without JS; with JS only data-open rows start expanded ----
  document.querySelectorAll('[data-abr-expand]').forEach(function (btn) {
    var row = document.getElementById(btn.getAttribute('aria-controls'));
    if (!row) { return; }
    var set = function (open) { row.hidden = !open; btn.setAttribute('aria-expanded', String(open)); };
    set(row.getAttribute('data-open') === 'true');
    btn.addEventListener('click', function () { set(row.hidden); });
  });

  // ---- Mock failure switch (admin): the switch only edits the count field; the count is what is saved ----
  document.querySelectorAll('[data-abr-failswitch]').forEach(function (sw) {
    var input = document.getElementById(sw.getAttribute('aria-controls'));
    if (!input) { return; }
    sw.addEventListener('change', function () {
      input.value = sw.checked ? String(Math.max(1, Number(input.value) || 0)) : '0';
    });
    input.addEventListener('input', function () { sw.checked = Number(input.value) > 0; });
  });
})();
