// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


// Reveal the floating Back button only after scrolling down.
(() => {
  const backButton = document.getElementById('floatingBackButton');
  if (!backButton) return;

  const updateBackButton = () => {
    backButton.classList.toggle('is-visible', window.scrollY > 120);
  };

  window.addEventListener('scroll', updateBackButton, { passive: true });
  updateBackButton();
})();

// Show the floating Top button alongside Back after scrolling down.
(() => {
  const topButton = document.getElementById('floatingTopButton');
  if (!topButton) return;
  const updateTopButton = () => topButton.classList.toggle('is-visible', window.scrollY > 120);
  window.addEventListener('scroll', updateTopButton, { passive: true });
  updateTopButton();
})();


// Full-page readiness gate: keep Farm hidden until the page and its async data have settled.
(() => {
  const overlay = document.getElementById('farmLoadingOverlay');
  if (!overlay) return;

  let pendingFetches = 0;
  let navigationTimer;
  const originalFetch = window.fetch;

  window.fetch = function (...args) {
    pendingFetches++;
    return originalFetch.apply(this, args).finally(() => {
      pendingFetches = Math.max(0, pendingFetches - 1);
    });
  };

  const show = () => {
    clearTimeout(navigationTimer);
    overlay.classList.remove('is-hidden');
    overlay.setAttribute('aria-hidden', 'false');
  };

  const hide = () => {
    overlay.classList.add('is-hidden');
    overlay.setAttribute('aria-hidden', 'true');
  };

  const imagesReady = () => Promise.all(
    Array.from(document.images).map(img => img.complete
      ? Promise.resolve()
      : new Promise(resolve => {
          img.addEventListener('load', resolve, { once: true });
          img.addEventListener('error', resolve, { once: true });
        }))
  );

  const waitForPageReady = async () => {
    if (document.readyState !== 'complete') {
      await new Promise(resolve => window.addEventListener('load', resolve, { once: true }));
    }
    if (document.fonts?.ready) await document.fonts.ready.catch(() => {});
    await imagesReady();

    const deadline = Date.now() + 15000;
    while (pendingFetches > 0 && Date.now() < deadline) {
      await new Promise(resolve => setTimeout(resolve, 50));
    }

    // Give charts, cards and fetched article content time to finish painting.
    await new Promise(requestAnimationFrame);
    await new Promise(requestAnimationFrame);
    await new Promise(resolve => setTimeout(resolve, 2000));
    hide();
  };

  const startReadinessGate = () => {
    show();
    const pageReady = window.farmConditionsReady;
    if (pageReady && typeof pageReady.then === 'function') {
      pageReady.catch(() => {}).finally(waitForPageReady);
    } else {
      waitForPageReady();
    }
  };

  // Page-specific readiness promises are declared by the page's Scripts section,
  // which is rendered after this shared script. Wait until DOMContentLoaded so
  // those promises exist before deciding the page is ready.
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', startReadinessGate, { once: true });
  } else {
    startReadinessGate();
  }

  document.addEventListener('submit', event => {
    if (event.target instanceof HTMLFormElement && event.target.method.toLowerCase() !== 'dialog') show();
  });

  document.addEventListener('click', event => {
    const link = event.target.closest('a[href]');
    if (!link || link.target === '_blank' || link.hasAttribute('download') || link.origin !== window.location.origin) return;
    if (link.pathname !== window.location.pathname || link.search !== window.location.search) show();
  });
})();
