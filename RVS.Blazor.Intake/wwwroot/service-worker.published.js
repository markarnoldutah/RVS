// Import the build-generated asset manifest (self.assetsManifest).
// .NET 10 SDK no longer prepends this automatically during publish.
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/];
const offlineAssetsExclude = [/^service-worker\.js$/, /^staticwebapp\.config\.json$/, /^appsettings.*\.json$/];

// Skip waiting so the new service worker activates immediately after install,
// ensuring updated WASM files are served on the next navigation.
self.addEventListener('message', event => {
    if (event.data === 'skipWaiting') self.skipWaiting();
});

async function onInstall(event) {
    console.info('Service worker: Install');

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => asset.hash && asset.hash !== 'sha256-47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=')
        .filter(asset => !asset.url.endsWith('staticwebapp.config.json'))
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete old caches so only the current version remains
    const cacheKeys = await caches.keys();
    await Promise.all(
        cacheKeys
            .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
            .map(key => caches.delete(key))
    );

    // Take control of all open tabs immediately so cached assets
    // are served without requiring a second navigation.
    // Mark: debug
    //await self.clients.claim();
}

async function onFetch(event) {
    let cachedResponse = null;

    if (event.request.method === 'GET') {
        // For navigation requests, serve the cached index.html (SPA routing)
        const shouldServeIndexHtml = event.request.mode === 'navigate';
        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    return cachedResponse || fetch(event.request);
}

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

