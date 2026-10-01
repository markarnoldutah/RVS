// Manager app service worker (issue #498). It exists so the app is installable as a PWA —
// one tap from the home screen or taskbar.
//
// Deliberately network-only: no offline cache. Offline use is out of scope (Spec,
// "Explicitly out of scope"), and caching index.html or _framework assets would risk serving
// a stale build after a deploy. Every request goes straight to the network.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });
