// Starts Blazor and registers the service worker (moved out of inline <script>s so the
// Content-Security-Policy can forbid inline script). Must load after blazor.webassembly.js.
(function () {
    // .NET 10: blazor.boot.json was inlined into dotnet.js, so the blazor-environment response
    // header is no longer read. Detect the environment at runtime from the hostname instead.
    var hostname = window.location.hostname;
    var env = 'Production';
    if (hostname.includes('localhost') || hostname.includes('127.0.0.1')) {
        env = 'Development';
    } else if (hostname.includes('mango') || hostname.includes('staging')) {
        env = 'Staging';
    }
    Blazor.start({ environment: env });

    // Installable PWA (issue #498) — network-only worker, no offline cache.
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
    }
})();
