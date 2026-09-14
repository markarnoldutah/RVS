// Application Insights JS SDK v3 bootstrap (moved out of an inline <script> so the
// Content-Security-Policy can forbid inline script). The connection string is read from the
// Blazor appsettings.json at startup; with none configured, telemetry stays off.
(async function () {
    try {
        var resp = await fetch('appsettings.json');
        if (!resp.ok) return;
        var cfg = await resp.json();
        var cs = (cfg.ApplicationInsights && cfg.ApplicationInsights.ConnectionString) || "";
        if (!cs) return;
        var sdkInstance = new Microsoft.ApplicationInsights.ApplicationInsights({
            config: { connectionString: cs }
        });
        sdkInstance.loadAppInsights();
        sdkInstance.trackPageView();
        window.appInsights = sdkInstance;
    } catch (e) { /* appsettings.json may not exist in dev */ }
})();
