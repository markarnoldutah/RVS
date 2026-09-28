/**
 * Triggers a click event on the specified HTML element.
 * Used to programmatically open file inputs (e.g., camera capture).
 * @param {HTMLElement} element - The element to click.
 */
window.rvs_triggerClick = function (element) {
    if (element && typeof element.click === 'function') {
        element.click();
    }
};

/**
 * Audio recording state for speech-to-text capture.
 * @private
 */
window._rvs_mediaRecorder = null;
window._rvs_audioChunks = [];
window._rvs_negotiatedMimeType = '';

/**
 * Negotiates the best audio MIME type supported by the browser for Azure Speech compatibility.
 * Prefers audio/ogg;codecs=opus (natively supported by Azure Speech REST API v1).
 * Falls back to audio/webm variants if OGG is unavailable.
 * @returns {string} The best supported MIME type, or empty string if none matched.
 * @private
 */
function _rvs_negotiateMimeType() {
    var preferred = [
        'audio/ogg; codecs=opus',
        'audio/webm; codecs=opus',
        'audio/webm'
    ];
    for (var i = 0; i < preferred.length; i++) {
        if (MediaRecorder.isTypeSupported(preferred[i])) {
            return preferred[i];
        }
    }
    return '';
}

/**
 * Starts recording audio from the user's microphone.
 * Requests microphone permission and begins capturing audio in the best format
 * supported by the browser for Azure Speech compatibility (preferring OGG Opus).
 * @returns {Promise<void>}
 */
window.rvs_startRecording = async function () {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    window._rvs_audioChunks = [];
    window._rvs_negotiatedMimeType = _rvs_negotiateMimeType();
    var recorderOptions = window._rvs_negotiatedMimeType
        ? { mimeType: window._rvs_negotiatedMimeType }
        : {};
    window._rvs_mediaRecorder = new MediaRecorder(stream, recorderOptions);

    window._rvs_mediaRecorder.ondataavailable = function (event) {
        if (event.data.size > 0) {
            window._rvs_audioChunks.push(event.data);
        }
    };

    window._rvs_mediaRecorder.start();
};

/**
 * Stops recording and returns the captured audio as a base64-encoded string
 * along with the MIME type used during recording.
 * Releases the microphone after stopping.
 * @returns {Promise<{audio: string|null, mimeType: string}>} Base64-encoded audio data and MIME type.
 */
window.rvs_stopRecording = function () {
    return new Promise(function (resolve) {
        if (!window._rvs_mediaRecorder || window._rvs_mediaRecorder.state === 'inactive') {
            resolve({ audio: null, mimeType: '' });
            return;
        }

        window._rvs_mediaRecorder.onstop = function () {
            var mimeType = window._rvs_negotiatedMimeType || 'audio/webm';
            const blob = new Blob(window._rvs_audioChunks, { type: mimeType });
            window._rvs_audioChunks = [];

            // Release microphone tracks
            if (window._rvs_mediaRecorder && window._rvs_mediaRecorder.stream) {
                window._rvs_mediaRecorder.stream.getTracks().forEach(function (t) { t.stop(); });
            }
            window._rvs_mediaRecorder = null;

            const reader = new FileReader();
            reader.onloadend = function () {
                // Strip the data URL prefix to get raw base64
                var base64 = reader.result;
                if (base64 && typeof base64 === 'string') {
                    var idx = base64.indexOf(',');
                    if (idx >= 0) {
                        base64 = base64.substring(idx + 1);
                    }
                }
                resolve({ audio: base64 || null, mimeType: mimeType });
            };
            reader.readAsDataURL(blob);
        };

        window._rvs_mediaRecorder.stop();
    });
};

/**
 * Attaches an iOS soft-keyboard guard to the adornment button of a MudDatePicker
 * located inside the wrapper with the given id.
 *
 * Without this guard, tapping the calendar icon on iOS either keeps the prior
 * keyboard up or causes the input to focus (spawning the keyboard), which
 * pushes the date picker popover off-screen.
 *
 * The guard blurs the active element on pointerdown/touchstart over the
 * adornment and briefly applies `readonly` to the text input so iOS will
 * not open the keyboard while MudDatePicker processes the click. Tapping
 * the input directly is unaffected, so the keyboard still spawns there.
 *
 * Safe to call repeatedly; the handler is only attached once per adornment.
 * @param {string} wrapperId - The id of the wrapping element around the MudDatePicker.
 */
window.rvs_attachDatePickerKeyboardGuard = function (wrapperId) {
    var wrapper = document.getElementById(wrapperId);
    if (!wrapper) return;

    var attach = function () {
        var adornment = wrapper.querySelector('.mud-input-adornment button');
        var input = wrapper.querySelector('input');
        if (!adornment || !input) return false;
        if (adornment.dataset.rvsKbGuard === '1') return true;
        adornment.dataset.rvsKbGuard = '1';

        var handler = function () {
            if (document.activeElement && typeof document.activeElement.blur === 'function') {
                document.activeElement.blur();
            }
            input.setAttribute('readonly', 'readonly');
            setTimeout(function () { input.removeAttribute('readonly'); }, 500);
        };
        adornment.addEventListener('pointerdown', handler, true);
        adornment.addEventListener('touchstart', handler, { capture: true, passive: true });
        return true;
    };

    if (attach()) return;

    var observer = new MutationObserver(function () {
        if (attach()) observer.disconnect();
    });
    observer.observe(wrapper, { childList: true, subtree: true });
    setTimeout(function () { observer.disconnect(); }, 5000);
};

/**
 * Keeps the page at the top for a moment after a wizard step change (issue #758). Continue on a
 * long step is usually pressed with the soft keyboard still up from the last text field — Step 2's
 * phone, Step 5's description. The keyboard closes as that field leaves the DOM, and mobile
 * browsers re-scroll while the viewport resizes back, which undid the scroll to the top. So the
 * scroll is reasserted on every viewport resize and scroll for a short window, and the hold is
 * dropped the moment the customer touches, wheels or presses a key, so it never fights them.
 * @private
 */
function _rvs_holdScrollAtTop() {
    var holdMs = 700;
    var released = false;
    var vv = window.visualViewport;

    var reassert = function () {
        if (!released && (window.scrollY !== 0 || (vv && vv.offsetTop !== 0))) {
            window.scrollTo(0, 0);
        }
    };
    var release = function () {
        if (released) return;
        released = true;
        window.removeEventListener('scroll', reassert);
        if (vv) {
            vv.removeEventListener('resize', reassert);
            vv.removeEventListener('scroll', reassert);
        }
        ['touchstart', 'wheel', 'keydown', 'pointerdown'].forEach(function (type) {
            window.removeEventListener(type, release, true);
        });
    };

    window.addEventListener('scroll', reassert, { passive: true });
    if (vv) {
        vv.addEventListener('resize', reassert);
        vv.addEventListener('scroll', reassert);
    }
    ['touchstart', 'wheel', 'keydown', 'pointerdown'].forEach(function (type) {
        window.addEventListener(type, release, { capture: true, passive: true });
    });
    requestAnimationFrame(reassert);
    setTimeout(release, holdMs);
}

/**
 * Enters an intake wizard step (issues #645, #766): scrolls to the top of the page and holds it
 * there, then focuses the step container so keyboard and screen-reader users start on the new step
 * rather than on the removed button. No form field is focused: on a phone that raised the soft
 * keyboard, and the browser's scroll to the focused field fought the scroll to the top — the
 * jitter on Steps 2 and 5 (issue #766). The container carries tabindex="-1" and is focused with
 * preventScroll, so it neither opens the keyboard nor moves the page.
 * @param {HTMLElement} stepElement - The wizard step container; carries tabindex="-1".
 */
window.rvs_enterWizardStep = function (stepElement) {
    window.scrollTo(0, 0);
    _rvs_holdScrollAtTop();
    if (stepElement) stepElement.focus({ preventScroll: true });
};

/**
 * Steps one entry back in the browser's session history — what the browser's own Back button
 * does. The intake wizard's Back button calls this so the two are the same action: the wizard
 * step it lands on comes from the history entry, and the entry left behind stays available to
 * the Forward button. The host only calls this when it knows it pushed an entry of its own in
 * this page's lifetime, so this cannot walk the customer off the form.
 */
window.rvs_historyBack = function () {
    window.history.back();
};

/**
 * Marks a file picker as open, so a page reloaded before it came back can say so (issue #736).
 * On a phone short of memory, Android can kill the tab while the camera app is in front, and the
 * photo is lost with it. Set on the click itself, in plain JS: a round trip through .NET might not
 * finish before the camera takes over. Cleared when the input reports a choice or a cancel.
 * @private
 */
var _rvs_pickerOpenKey = 'rvs_picker_open_at';

function _rvs_isFileInput(target) {
    return target && target.tagName === 'INPUT' && target.type === 'file';
}

function _rvs_clearPickerOpen(event) {
    if (!_rvs_isFileInput(event.target)) return;
    try { sessionStorage.removeItem(_rvs_pickerOpenKey); } catch (e) { /* storage blocked */ }
}

document.addEventListener('click', function (event) {
    if (!_rvs_isFileInput(event.target)) return;
    try { sessionStorage.setItem(_rvs_pickerOpenKey, String(Date.now())); } catch (e) { /* storage blocked */ }
}, true);
document.addEventListener('change', _rvs_clearPickerOpen, true);
document.addEventListener('cancel', _rvs_clearPickerOpen, true);

// A mark present as this script loads was left by the page before this one: the picker never came
// back to it. Read once here, so a stale mark can never be mistaken for a reload later on.
var _rvs_pickerOpenAtLoad = (function () {
    try {
        var openedAt = Number(sessionStorage.getItem(_rvs_pickerOpenKey));
        sessionStorage.removeItem(_rvs_pickerOpenKey);
        return openedAt;
    } catch (e) {
        return 0;
    }
})();

/**
 * Whether this page load replaced one that had a file picker open. Reports it once. A mark older
 * than ten minutes is treated as stale: a picker dismissed on a browser without the cancel event
 * leaves one behind, and a reload long after is not the picker's doing.
 * @returns {boolean}
 */
window.rvs_takePickerReload = function () {
    var openedAt = _rvs_pickerOpenAtLoad;
    _rvs_pickerOpenAtLoad = 0;
    return openedAt > 0 && Date.now() - openedAt < 10 * 60 * 1000;
};
