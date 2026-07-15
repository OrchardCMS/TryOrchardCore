// Theme chrome behaviour. The initial dark/light mode is applied before paint by
// the inline script in <head>; this file wires the interactive parts.
(function () {
  'use strict';

  // --- Scroll reveal: fade + rise each section's content as it enters the viewport. The `.js .reveal`
  // rules in site.css hide it first; this reveals it once. Runs FIRST so an error in a later block can
  // never leave content stuck hidden. Degrades safely: without IntersectionObserver (or without JS at
  // all) everything is shown. One-shot (unobserve) so it doesn't replay on scroll-up.
  (function () {
    var items = document.querySelectorAll('.reveal');
    if (!items.length) return;
    if (!('IntersectionObserver' in window)) {
      items.forEach(function (el) { el.classList.add('is-visible'); });
      return;
    }
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.isIntersecting) { entry.target.classList.add('is-visible'); observer.unobserve(entry.target); }
      });
    }, { threshold: 0.12 });
    items.forEach(function (el) { observer.observe(el); });
  })();

  // --- Dark / light mode toggle: persist the choice and keep the label in sync.
  (function () {
    var root = document.documentElement;
    var btn = document.getElementById('themeToggle');
    if (!btn) return;

    function sync() {
      var dark = root.classList.contains('dark');
      btn.setAttribute('aria-checked', dark ? 'true' : 'false');
      btn.setAttribute('aria-label', dark ? 'Switch to light mode' : 'Switch to dark mode');
    }
    sync();

    btn.addEventListener('click', function () {
      var dark = root.classList.toggle('dark');
      try { localStorage.setItem('ocTheme', dark ? 'dark' : 'light'); } catch (error) {}
      sync();
    });
  })();

  // --- Header: deepen the shadow once the page is scrolled.
  (function () {
    var header = document.getElementById('siteHeader');
    if (!header) return;
    function onScroll() { header.classList.toggle('is-stuck', window.scrollY > 8); }
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
  })();

  // --- Mobile menu: a fixed overlay panel with a dimmed backdrop, laid over the page (it does not
  // push content down). Body scroll is locked while open. Closes on link selection, backdrop click,
  // or Escape.
  (function () {
    var btn = document.getElementById('menuBtn');
    var nav = document.getElementById('mobileNav');
    var backdrop = document.getElementById('mobileNavBackdrop');
    var header = document.getElementById('siteHeader');
    if (!btn || !nav || !backdrop) return;

    var icons = btn.querySelectorAll('svg'); // [0] = menu (open), [1] = close (X)

    function isOpen() { return !nav.classList.contains('hidden'); }

    function openMenu() {
      // Anchor the panel just below the actual header height.
      if (header) document.documentElement.style.setProperty('--header-h', header.offsetHeight + 'px');
      nav.classList.remove('hidden');
      backdrop.classList.remove('hidden');
      btn.setAttribute('aria-expanded', 'true');
      btn.setAttribute('aria-label', 'Close menu');
      if (icons[0]) icons[0].classList.add('hidden');
      if (icons[1]) icons[1].classList.remove('hidden');
      document.body.style.overflow = 'hidden';
    }

    function closeMenu() {
      nav.classList.add('hidden');
      backdrop.classList.add('hidden');
      btn.setAttribute('aria-expanded', 'false');
      btn.setAttribute('aria-label', 'Open menu');
      if (icons[0]) icons[0].classList.remove('hidden');
      if (icons[1]) icons[1].classList.add('hidden');
      document.body.style.overflow = '';
    }

    btn.addEventListener('click', function () { isOpen() ? closeMenu() : openMenu(); });
    backdrop.addEventListener('click', closeMenu);
    nav.addEventListener('click', function (event) { if (event.target.closest('a')) closeMenu(); });
    document.addEventListener('keydown', function (event) { if (event.key === 'Escape' && isOpen()) closeMenu(); });
  })();

  // --- The create-a-site form: details, then starting point. Without JS both steps are visible and
  // the form still submits in one go (the `hidden` class only lands on step 2 once we take over), so
  // this is a progressive enhancement. The server re-renders this page on a validation failure and
  // marks the step the error is on via data-initial-step, so the user lands where the problem is.
  (function () {
    var form = document.querySelector('[data-create-form]');
    if (!form) return;

    var step1 = form.querySelector('[data-step="1"]');
    var step2 = form.querySelector('[data-step="2"]');
    var label = form.querySelector('[data-step-label]');
    var bar = form.querySelector('[data-step-bar]');
    var next = form.querySelector('[data-next]');
    var back = form.querySelector('[data-back]');
    if (!step1 || !step2 || !next || !back) return;

    function show(step, focus) {
      var on2 = step === 2;
      step1.classList.toggle('hidden', on2);
      step2.classList.toggle('hidden', !on2);
      if (label) label.textContent = label.getAttribute('data-step-label').replace('{0}', step);
      if (bar) bar.style.width = on2 ? '100%' : '50%';
      if (focus) (on2 ? step2 : step1).focus();
    }

    next.addEventListener('click', function () {
      // Only advance once the step 1 fields are valid, so a missing email is caught here rather
      // than after the user has picked a starting point.
      var fields = step1.querySelectorAll('input');
      for (var i = 0; i < fields.length; i++) {
        if (!fields[i].checkValidity()) { fields[i].reportValidity(); return; }
      }
      show(2, true);
    });

    back.addEventListener('click', function () { show(1, true); });

    // --- Create the site without leaving the page: post with fetch, show a loading state, then swap
    // the form for the success panel in the same card. The controller answers this request with JSON
    // (it sees the X-Requested-With header). Without JS none of this runs and the form posts normally,
    // so it degrades to the server redirect.
    var success = document.querySelector('[data-create-success]');
    var submitBtn = form.querySelector('button[type="submit"]');
    var errorBox = form.querySelector('[data-create-error]');
    var step1Fields = ['SiteName', 'Email'];

    function clearErrors() {
      form.querySelectorAll('[data-valmsg-for]').forEach(function (el) { el.textContent = ''; });
      if (errorBox) { errorBox.hidden = true; errorBox.textContent = ''; }
    }

    function showGeneralError() {
      if (!errorBox) return;
      errorBox.textContent = form.getAttribute('data-error-text') || 'Something went wrong.';
      errorBox.hidden = false;
    }

    // Drop each server error into its field's message slot; anything without a slot falls back to the
    // general error line. Jump to the earliest step that has an error so it is on screen.
    function showFieldErrors(errors) {
      clearErrors();
      var keys = Object.keys(errors || {});
      if (!keys.length) { showGeneralError(); return; }
      var targetStep = 2;
      keys.forEach(function (key) {
        var slot = form.querySelector('[data-valmsg-for="' + key + '"]');
        if (slot) { slot.textContent = errors[key]; }
        else { showGeneralError(); }
        if (step1Fields.indexOf(key) !== -1) { targetStep = 1; }
      });
      show(targetStep, true);
    }

    if (submitBtn) {
      form.addEventListener('submit', function (event) {
        event.preventDefault();

        // Jump to the step of the first invalid field so its validation bubble is visible, not hidden.
        if (!form.checkValidity()) {
          var invalid = form.querySelector(':invalid');
          if (invalid) { show(step2.contains(invalid) ? 2 : 1, false); }
          form.reportValidity();
          return;
        }

        var label = submitBtn.innerHTML;
        submitBtn.disabled = true;
        submitBtn.textContent = submitBtn.getAttribute('data-loading') || 'Creating your site…';
        clearErrors();

        fetch(form.action, {
          method: 'POST',
          headers: { 'X-Requested-With': 'XMLHttpRequest' },
          body: new FormData(form)
        }).then(function (response) {
          return response.json().catch(function () { return null; });
        }).then(function (data) {
          if (data && data.success) {
            form.hidden = true;
            if (success) success.hidden = false;
            return;
          }
          submitBtn.disabled = false;
          submitBtn.innerHTML = label;
          if (data && data.errors) { showFieldErrors(data.errors); }
          else { showGeneralError(); }
        }).catch(function () {
          submitBtn.disabled = false;
          submitBtn.innerHTML = label;
          showGeneralError();
        });
      });
    }

    // Take over: hide the step the user is not on. Don't steal focus on first paint.
    show(form.getAttribute('data-initial-step') === '2' ? 2 : 1, false);
  })();

  // --- Accordions (FAQ): a double/triple-click on a <summary> toggles it but also selects the
  // heading text, which looks odd. Suppress selection from multi-clicks only — single clicks still
  // toggle, and deliberate drag-selection (so the text stays copyable) is left untouched.
  (function () {
    document.querySelectorAll('details > summary').forEach(function (summary) {
      summary.addEventListener('mousedown', function (event) {
        if (event.detail > 1) {
          event.preventDefault();
        }
      });
    });
  })();

  // --- Scroll-spy: highlight the header nav link for the section currently in view. Nav links carry
  // data-spy="<section id>"; desktop and mobile links share the same ids, so both stay in sync.
  (function () {
    var links = Array.prototype.slice.call(document.querySelectorAll('.nav-link[data-spy]'));
    if (!links.length || !('IntersectionObserver' in window)) return;
    function setCurrent(id) {
      links.forEach(function (l) { l.classList.toggle('is-current', l.getAttribute('data-spy') === id); });
    }
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) { if (e.isIntersecting) setCurrent(e.target.id); });
    }, { rootMargin: '-12% 0px -80% 0px', threshold: 0 });
    var seen = {};
    links.forEach(function (l) {
      var id = l.getAttribute('data-spy');
      if (seen[id]) { return; }
      seen[id] = true;
      var sec = document.getElementById(id);
      if (sec) { io.observe(sec); }
    });
  })();
})();
