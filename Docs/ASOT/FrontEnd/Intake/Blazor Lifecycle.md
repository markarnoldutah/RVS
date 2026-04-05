# RVS.Blazor.Intake — Standalone WASM PWA Lifecycle

`RVS.Blazor.Intake` is a **standalone Blazor WebAssembly PWA**. There is no server-side rendering, no Blazor circuit, and no SignalR connection. All pages (landing, wizard, confirmation, status) are client-side routes within a single SPA.

---

## First Visit (cold — no service worker cache)

```
GET https://app.rvserviceflow.com/intake/blue-compass-salt-lake
 └─ Azure Static Web Apps CDN
     └─ Returns index.html (static shell — no .NET, just HTML + blazor.webassembly.js link)
 └─ Browser downloads blazor.webassembly.js
 └─ blazor.webassembly.js downloads .NET WASM runtime + app DLLs (~5–15 MB)
 └─ Service worker installs, caches WASM runtime and app assets
 └─ .NET runtime boots in browser WebAssembly sandbox
 └─ Program.cs runs (DI container built, HttpClient registered, routing configured)
 └─ Router resolves /intake/blue-compass-salt-lake → IntakeLanding.razor
     └─ OnInitializedAsync: GET api/intake/{slug} → dealer config loaded
     └─ Page renders: dealer logo, name, "Start Service Request" button
```

---

## Subsequent Visits (warm — service worker cache active)

```
GET https://app.rvserviceflow.com/intake/blue-compass-salt-lake
 └─ Service worker intercepts request
     └─ Serves WASM runtime + app DLLs from browser cache (no network download)
 └─ .NET runtime boots (near-instant from cache)
 └─ Router resolves route → component renders
```

---

## In-SPA Navigation (all client-side, no page reloads)

```
User taps "Start Service Request"
 └─ NavigationManager.NavigateTo("/intake/{slug}/start")
     └─ Router resolves → IntakeWizard.razor (Step 1)
     └─ IntakeWizardState (scoped service) initializes / restores from sessionStorage
     └─ No network request, no server round-trip

User completes wizard → submits
 └─ POST api/intake/{slug}/service-requests
     └─ 201 response with requestId + magic-link token
 └─ NavigationManager.NavigateTo("/intake/{slug}/confirmation")
     └─ ConfirmationPage.razor renders (client-side, reads state from IntakeWizardState)

User opens magic-link
 └─ GET https://app.rvserviceflow.com/status/{token}
     └─ Service worker serves WASM from cache (if previously visited)
     └─ Router resolves → StatusPage.razor
         └─ OnInitializedAsync: GET api/status/{token} → SR summary loaded
         └─ Page renders all active SRs across dealerships
```

---

## Key Differences from the Old SSR+WASM Hybrid

| Aspect | Old (removed) | Current |
|---|---|---|
| Landing page | Static SSR (server-rendered HTML) | WASM SPA client-side route |
| Status page | Static SSR (server-rendered HTML) | WASM SPA client-side route |
| Wizard | `@rendermode InteractiveWebAssembly` (hydrated from SSR) | Full WASM, no hydration |
| WASM preload | `<link rel="modulepreload">` on SSR page | Service worker caches runtime after first load |
| Server process | Required for SSR pages | None — static CDN only |
| SignalR | Not used, but SSR pipeline was present | Completely absent |
