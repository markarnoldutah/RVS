// Sets up the loading screen before Blazor starts, via two attributes on <html> that the
// splash CSS in index.html reads. It lives here rather than in an inline <script> so the
// Content-Security-Policy can forbid inline script.
//
//   data-splash  full | minimal — the full branded splash on a cold load; a bare progress
//                bar when the WASM runtime is already cached (the return from the auth
//                callback), so one sign-in does not show two splashes.
//
//   data-theme   light | dark | highcontrast — the theme the user last chose. The splash
//                paints long before MudThemeProvider exists, so without this a dark-mode
//                user got a light ground that repainted the moment the app booted (#703).
//
// The storage key is ThemeService.ThemeStorageKey and the values are what SetModeAsync
// writes; keep this in step with Services/ThemeService.cs.
(function () {
    var html = document.documentElement;

    var isReturn = false;
    try {
        isReturn = sessionStorage.getItem('rvs-wasm-loaded') === '1';
        if (!isReturn) sessionStorage.setItem('rvs-wasm-loaded', '1');
    } catch (e) { }
    html.setAttribute('data-splash', isReturn ? 'minimal' : 'full');

    var theme = 'light';
    try {
        var saved = localStorage.getItem('rvs-manager-theme-preference');
        if (saved === 'dark' || saved === 'highcontrast') theme = saved;
    } catch (e) { }
    html.setAttribute('data-theme', theme);
})();
