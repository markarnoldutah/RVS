# ADR-001 — Manager App: No PWA

## Status

Accepted

## Date

2026-04-05

## Context

`RVS.Blazor.Manager` is a Blazor WebAssembly application used by dealership service managers,
service advisors, and operations leadership to triage intake requests, manage work assignments,
monitor the service board, and review analytics.

The question considered was whether to configure the Manager app as a **Progressive Web App
(PWA)** — adding a Web App Manifest, application icons, and a service worker for asset caching
and offline support — as was done for `RVS.Blazor.Intake`.

## Decision

**PWA will not be implemented in the Manager app.** The app intentionally runs as a standard
Blazor WASM application with no service worker and no Web App Manifest.

## Rationale

| Concern | Detail |
|---|---|
| **Authentication conflict** | Manager authenticates via Microsoft Entra ID (MSAL). MSAL uses browser redirects and silent-refresh iframes. Service workers that intercept `fetch` events can break these flows, requiring complex opt-out logic and ongoing maintenance. |
| **Network environment** | Manager users (service managers, advisors, leadership) operate on reliable dealership Wi-Fi or wired office networks. There is no meaningful benefit to offline caching for this audience. |
| **Real-time data requirements** | The Service Board polls for live status changes every five minutes (with SignalR planned for vNEXT). Cached responses served by a service worker would display stale queue and board data without additional cache-invalidation coordination, reducing the value of the real-time design. |
| **Screen and usage profile** | The Manager is optimized for large screens and continuous operational oversight. "Install to home screen" — a primary PWA benefit — has low ROI for a desktop-first internal business tool. |
| **Cache invalidation overhead** | WASM bundles (.dll, .wasm) are large. A service worker caches these on install and only evicts them on a new service worker version. This means users can get stuck on a stale version until the browser runs the background update cycle, which is particularly painful for a frequently-deployed internal app. |
| **Push notifications** | Push notification delivery requires a server-side notification infrastructure (Web Push Protocol, VAPID keys, subscription storage). For MVP this is out of scope, and MSAL sessions provide adequate session continuity without it. |

## Alternatives Considered

### Add PWA but exclude API calls from cache

A network-first (or cache-network-race) strategy could be applied only to static assets while
passing all API and authentication requests through to the network. This reduces the
authentication conflict risk but retains all other drawbacks: large WASM bundle cache, stale
UI risk, and significant configuration complexity with marginal benefit for the target audience.

### Defer to vNEXT

Push notifications could be added in a later phase when the SignalR hub is implemented,
allowing real-time delivery of status change alerts. At that point the trade-offs should be
re-evaluated. The decision can be revisited as a follow-up ADR if the Manager expands to
tablet or field-technician use cases where install-to-home-screen becomes valuable.

## Contrast with Intake App

`RVS.Blazor.Intake` IS configured as a PWA because:

- It targets anonymous public users (customers) on potentially unreliable mobile networks.
- The WASM runtime, once cached, makes repeat visits near-instant — a meaningful UX benefit for
  one-time or infrequent visitors who may return via a magic link.
- No MSAL authentication flows to conflict with.
- The intake wizard is a one-session linear flow; cached assets do not risk presenting stale operational data.

## Consequences

- `RVS.Blazor.Manager` has no `manifest.webmanifest`, no application icons beyond `favicon.ico`,
  and no `service-worker.js` / `service-worker.published.js`.
- The Manager `index.html` does not register a service worker.
- The Manager `.csproj` does not reference `ServiceWorkerAssetsManifest` or related build targets.
- Future changes to this decision should be captured in a follow-up ADR that supersedes this one.
