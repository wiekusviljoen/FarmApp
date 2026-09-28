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
