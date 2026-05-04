// Auth refresh shim — bridges Microsoft's Blazor WASM Authentication library to
// Auth0's /oauth/token endpoint so access tokens can be renewed via the long-lived
// refresh_token (15-day rolling per RVS_Technical_PRD.md §10.1) instead of the
// default iframe silent-renewal path, which fails under third-party-cookie blocking
// (Safari ITP, Chrome 3rd-party-cookie phase-out) and is bounded by Auth0's
// 3-day idle SSO cookie.
//
// The Microsoft library stores user state in sessionStorage under a key that
// embeds the OIDC authority and client id. We read/refresh/rewrite that entry
// in place so the rest of the auth pipeline stays unchanged.

(function () {
    "use strict";

    // The Microsoft Blazor WASM Authentication library stores its user record
    // under a key that varies by version. Across .NET 8/9/10 the format has
    // been a JSON object containing: access_token, refresh_token, expires_at
    // (unix seconds), id_token, scope, token_type, profile.
    //
    // Find the entry by scanning sessionStorage for a key that starts with
    // "oidc.user:" or contains the configured client id — robust to minor
    // key-format changes between framework versions.
    function findUserStorageKey(clientId) {
        for (let i = 0; i < sessionStorage.length; i++) {
            const key = sessionStorage.key(i);
            if (!key) continue;
            if (key.startsWith("oidc.user:") && (!clientId || key.includes(clientId))) {
                return key;
            }
        }
        return null;
    }

    function readUser(clientId) {
        const key = findUserStorageKey(clientId);
        if (!key) return null;
        try {
            const raw = sessionStorage.getItem(key);
            if (!raw) return null;
            return { key: key, user: JSON.parse(raw) };
        } catch {
            return null;
        }
    }

    function writeUser(key, user) {
        sessionStorage.setItem(key, JSON.stringify(user));
    }

    // Returns the stored refresh_token, or null. Exposed for diagnostics.
    window.rvsAuth_getRefreshToken = function (clientId) {
        const entry = readUser(clientId);
        return entry?.user?.refresh_token ?? null;
    };

    // Returns the stored access_token + expires_at, or null. Used by the
    // C# decorator to decide whether a refresh is needed.
    window.rvsAuth_getStoredToken = function (clientId) {
        const entry = readUser(clientId);
        if (!entry?.user) return null;
        return {
            accessToken: entry.user.access_token ?? null,
            expiresAt: entry.user.expires_at ?? 0,
            refreshToken: entry.user.refresh_token ?? null,
            scope: entry.user.scope ?? ""
        };
    };

    // Apply a token-endpoint response to the cached user record so subsequent
    // RequestAccessToken() calls from the Microsoft library see the new token.
    //
    // tokenResponse shape (from Auth0 /oauth/token):
    //   { access_token, refresh_token?, expires_in, id_token?, scope?, token_type }
    window.rvsAuth_applyRefreshedToken = function (clientId, tokenResponse) {
        const entry = readUser(clientId);
        if (!entry) return false;

        const now = Math.floor(Date.now() / 1000);
        entry.user.access_token = tokenResponse.access_token;
        entry.user.expires_at = now + (tokenResponse.expires_in ?? 0);
        entry.user.token_type = tokenResponse.token_type ?? entry.user.token_type;
        if (tokenResponse.refresh_token) {
            // Refresh-token rotation is enabled in Auth0 — store the new one.
            entry.user.refresh_token = tokenResponse.refresh_token;
        }
        if (tokenResponse.id_token) {
            entry.user.id_token = tokenResponse.id_token;
        }
        if (tokenResponse.scope) {
            entry.user.scope = tokenResponse.scope;
        }
        writeUser(entry.key, entry.user);
        return true;
    };
})();
