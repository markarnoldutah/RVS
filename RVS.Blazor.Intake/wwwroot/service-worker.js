// Service worker temporarily disabled — unregister self
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', () => self.registration.unregister());
