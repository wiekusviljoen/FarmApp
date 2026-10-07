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


// FarmFlow loading indicator for navigation and form submissions.
(() => {
  const overlay = document.getElementById('farmLoadingOverlay');
  if (!overlay) return;
  let timer;
  const show = () => {
    clearTimeout(timer);
    timer = setTimeout(() => {
      overlay.classList.add('is-visible');
      overlay.setAttribute('aria-hidden', 'false');
    }, 120);
  };
  const hide = () => {
    clearTimeout(timer);
    overlay.classList.remove('is-visible');
    overlay.setAttribute('aria-hidden', 'true');
  };
  window.addEventListener('pageshow', hide);
  document.addEventListener('submit', event => {
    if (event.target instanceof HTMLFormElement && event.target.method.toLowerCase() !== 'dialog') show();
  });
  document.addEventListener('click', event => {
    const link = event.target.closest('a[href]');
    if (!link || link.target === '_blank' || link.hasAttribute('download') || link.origin !== window.location.origin) return;
    if (link.pathname !== window.location.pathname || link.search !== window.location.search) show();
  });
})();
