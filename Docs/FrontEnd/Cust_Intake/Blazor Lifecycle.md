GET /
 └─ Kestrel → middleware pipeline
     └─ MapRazorComponents<App>()
         └─ App.razor  [HTML shell, SSR]
             └─ Routes.razor  [Router scans server + client assemblies]
                 └─ MainLayout.razor  [FluentUI chrome, SSR]
                     └─ Home.razor  [Static SSR — no rendermode]
                         └─ <WasmPreload />  ← modulepreload hint fires
 └─ Complete HTML sent to browser
 └─ blazor.web.js executes (tiny, cached via preload)

User clicks "Start Service Request"  (data-enhance-nav="false" = full navigation)
 └─ GET /intake/{slug}
     └─ Server prerenders IntakeWizard  [InteractiveWebAssembly]
         └─ Blazor markers emitted in HTML
 └─ blazor.web.js detects WASM markers
     └─ Downloads dotnet.wasm + app DLLs
     └─ Boots .NET runtime in browser
     └─ RVS.Cust_Intake.Client/Program.cs runs  ← WASM DI container built
     └─ IntakeWizard hydrated  ← fully interactive
         └─ OnAfterRenderAsync: state restored, event wired
         └─ IntakeLanding.OnInitializedAsync: API call for dealer config