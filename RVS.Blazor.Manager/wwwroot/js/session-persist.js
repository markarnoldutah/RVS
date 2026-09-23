// Session persistence shim — keeps a manager signed in across browser and installed-PWA
// restarts (Spec C-7, issue #498), so tapping a status link in the packet email lands in
// the app without an interactive login.
//
// Microsoft's Blazor WASM Authentication library stores the OIDC user record (tokens,
// including the rotating refresh_token) in sessionStorage and exposes no option to change
// the store. sessionStorage dies with the tab, so every cold start meant a fresh login.
// This script mirrors that one record into localStorage and restores it on startup; the
// rest of the auth pipeline (including auth-refresh.js) keeps reading sessionStorage.
//
// Persistence is OPT-IN per device. Nothing is mirrored until the user answers "yes" to
// the KeepSignedInPrompt component; answering "no" (or never answering) keeps the old
// per-tab session. That keeps shared service-desk computers from staying signed in.
//
// It must load before AuthenticationService.js so the restore happens before the library
// first reads the user.
//
// Trade-off: an opted-in refresh token in localStorage is readable by script on this
// origin. It is bounded by the Content-Security-Policy (staticwebapp.config.json), Auth0
// refresh-token rotation with reuse detection, a 7-day idle / 30-day absolute lifetime,
// and revocation on sign-out.

(function () {
    "use strict";

    var USER_PREFIX = "oidc.user:";
    var MIRROR_PREFIX = "rvs.persist.";
    var PREFERENCE_KEY = "rvs.keepSignedIn"; // "yes" | "no" | absent (not asked yet)

    var proto = Storage.prototype;
    var getItem = proto.getItem;
    var setItem = proto.setItem;
    var removeItem = proto.removeItem;

    function isUserKey(key) {
        return typeof key === "string" && key.indexOf(USER_PREFIX) === 0;
    }

    function keysWithPrefix(store, prefix) {
        var keys = [];
        for (var i = 0; i < store.length; i++) {
            var key = store.key(i);
            if (key && key.indexOf(prefix) === 0) keys.push(key);
        }
        return keys;
    }

    function preference() {
        try { return getItem.call(window.localStorage, PREFERENCE_KEY); } catch (e) { return null; }
    }

    function removeMirrors() {
        try {
            keysWithPrefix(window.localStorage, MIRROR_PREFIX + USER_PREFIX).forEach(function (key) {
                removeItem.call(window.localStorage, key);
            });
        } catch (e) { }
    }

    function mirrorCurrentUser() {
        try {
            keysWithPrefix(window.sessionStorage, USER_PREFIX).forEach(function (key) {
                setItem.call(window.localStorage, MIRROR_PREFIX + key, getItem.call(window.sessionStorage, key));
            });
        } catch (e) { }
    }

    // Restore on startup — only for a device the user opted in. Anything else never leaves a
    // mirror behind (for example, after the user later said "no").
    if (preference() === "yes") {
        try {
            keysWithPrefix(window.localStorage, MIRROR_PREFIX + USER_PREFIX).forEach(function (mirrorKey) {
                var userKey = mirrorKey.substring(MIRROR_PREFIX.length);
                if (getItem.call(window.sessionStorage, userKey) === null) {
                    setItem.call(window.sessionStorage, userKey, getItem.call(window.localStorage, mirrorKey));
                }
            });
        } catch (e) {
            // Storage blocked (private mode, policy) — fall back to per-tab sessions.
        }
    } else {
        removeMirrors();
    }

    // Mirror every write of the user record (sign-in, token refresh) while opted in; every
    // removal (sign-out) clears the mirror regardless.
    proto.setItem = function (key, value) {
        setItem.call(this, key, value);
        if (this === window.sessionStorage && isUserKey(key) && preference() === "yes") {
            try { setItem.call(window.localStorage, MIRROR_PREFIX + key, value); } catch (e) { }
        }
    };

    proto.removeItem = function (key) {
        removeItem.call(this, key);
        if (this === window.sessionStorage && isUserKey(key)) {
            try { removeItem.call(window.localStorage, MIRROR_PREFIX + key); } catch (e) { }
        }
    };

    // ── Interop for KeepSignedInPrompt and LoginDisplay ──────────────────────

    // Returns "yes", "no", or null when this device has not been asked yet.
    window.rvsSession_getPersistPreference = function () {
        var value = preference();
        return value === "yes" || value === "no" ? value : null;
    };

    // Records the user's answer. "yes" mirrors the current sign-in immediately; "no" removes
    // any mirror.
    window.rvsSession_setPersist = function (persist) {
        try { setItem.call(window.localStorage, PREFERENCE_KEY, persist ? "yes" : "no"); } catch (e) { }
        if (persist) {
            mirrorCurrentUser();
        } else {
            removeMirrors();
        }
    };

    // Removes the persisted sign-in (sign-out). The device's preference is kept, so the user
    // is not asked again next time.
    window.rvsSession_clearPersisted = function () {
        removeMirrors();
    };
})();
