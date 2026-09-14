// Loading-screen mode (moved out of an inline <script> so the Content-Security-Policy can
// forbid inline script). On the first load we show the full branded splash. After the auth
// callback (same session) the WASM runtime is cached, so we show a minimal spinner instead to
// avoid a jarring double-splash.
(function () {
    var isReturn = sessionStorage.getItem('rvs-wasm-loaded') === '1';
    if (!isReturn) sessionStorage.setItem('rvs-wasm-loaded', '1');
    document.documentElement.setAttribute('data-splash', isReturn ? 'minimal' : 'full');
})();
