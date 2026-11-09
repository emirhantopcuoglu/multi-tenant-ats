// Applies the saved (or system) theme before the page paints, so dark-mode users don't see the light
// palette flash while the bundle loads. A file, not an inline script, because the CSP allows only
// scripts from our own origin. Mirrors ThemeProvider; 'ats-theme' must match THEME_STORAGE_KEY.
(function () {
  try {
    var saved = localStorage.getItem('ats-theme');
    var dark =
      saved === 'dark' || (!saved && window.matchMedia('(prefers-color-scheme: dark)').matches);
    if (dark) document.documentElement.classList.add('dark');
  } catch (e) {
    /* storage unavailable: default to light */
  }
})();
