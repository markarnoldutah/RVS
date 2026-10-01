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
 * In-page camera (issue #736). Photos and videos are taken inside the page rather than in the
 * phone's camera app: on a phone short of memory, Android kills the browser tab while the camera
 * app is in front, and the capture — with everything else attached — is lost. One camera at a
 * time; CameraInterop.cs is the .NET side.
 * @private
 */
var _rvs_camera = { stream: null, recorder: null, chunks: [], recordingType: '', lastPhoto: null };

function _rvs_cameraError(error) {
    var name = error && error.name;
    if (name === 'NotAllowedError' || name === 'SecurityError') return 'denied';
    if (name === 'NotFoundError' || name === 'OverconstrainedError') return 'no-camera';
    return 'failed';
}

/**
 * Starts the rear camera into a video element, replacing any camera already running. Photo mode
 * asks for as much resolution as the camera has; video mode asks for 720p, which keeps a clip
 * under the size limit, and for the microphone.
 * @param {HTMLVideoElement} video - The preview element.
 * @param {string} mode - 'photo' or 'video'.
 * @returns {Promise<string>} 'started', 'denied', 'no-camera', 'unsupported' or 'failed'.
 */
window.rvs_cameraStart = async function (video, mode) {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return 'unsupported';

    window.rvs_cameraStop();

    var withSound = mode === 'video';
    var videoConstraints = withSound
        ? { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } }
        : { facingMode: { ideal: 'environment' }, width: { ideal: 3840 }, height: { ideal: 2160 } };

    var stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia({ video: videoConstraints, audio: withSound });
    } catch (error) {
        if (!withSound || !error || error.name !== 'NotAllowedError') return _rvs_cameraError(error);
        // It may be the microphone that was refused; a silent clip beats none.
        try {
            stream = await navigator.mediaDevices.getUserMedia({ video: videoConstraints, audio: false });
        } catch (retryError) {
            return _rvs_cameraError(retryError);
        }
    }

    _rvs_camera.stream = stream;
    video.muted = true;
    video.setAttribute('playsinline', '');
    video.srcObject = stream;
    try { await video.play(); } catch (e) { /* autoplay attribute covers it */ }
    return 'started';
};

/**
 * Takes a JPEG of the frame the preview is showing, no larger than maxEdge on either edge.
 * Keeps the frame for rvs_cameraThumbnail.
 * @param {HTMLVideoElement} video - The preview element.
 * @param {number} maxEdge - Longest edge, in pixels.
 * @param {number} quality - JPEG quality, 0–1.
 * @returns {Promise<Uint8Array|null>}
 */
window.rvs_cameraTakePhoto = async function (video, maxEdge, quality) {
    var width = video && video.videoWidth;
    var height = video && video.videoHeight;
    if (!width || !height) return null;

    var scale = Math.min(1, maxEdge / Math.max(width, height));
    var canvas = document.createElement('canvas');
    canvas.width = Math.round(width * scale);
    canvas.height = Math.round(height * scale);
    canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);
    _rvs_camera.lastPhoto = canvas;

    var blob = await new Promise(function (resolve) { canvas.toBlob(resolve, 'image/jpeg', quality); });
    return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
};

/**
 * A small JPEG data URL of the photo rvs_cameraTakePhoto just took, then lets that frame go.
 * @param {number} maxEdge - Longest edge, in pixels.
 * @returns {string|null}
 */
window.rvs_cameraThumbnail = function (maxEdge) {
    var source = _rvs_camera.lastPhoto;
    _rvs_camera.lastPhoto = null;
    if (!source) return null;

    var scale = Math.min(1, maxEdge / Math.max(source.width, source.height));
    var canvas = document.createElement('canvas');
    canvas.width = Math.round(source.width * scale);
    canvas.height = Math.round(source.height * scale);
    canvas.getContext('2d').drawImage(source, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/jpeg', 0.8);
};

/**
 * The recording format: MP4 where the browser can (Safari; Chrome 126+), since it plays
 * everywhere a service manager might open it; WebM otherwise.
 * @private
 */
function _rvs_negotiateVideoType() {
    var preferred = ['video/mp4;codecs=avc1,mp4a', 'video/mp4', 'video/webm;codecs=vp8,opus', 'video/webm'];
    for (var i = 0; i < preferred.length; i++) {
        if (MediaRecorder.isTypeSupported(preferred[i])) return preferred[i];
    }
    return '';
}

/**
 * Starts recording the running camera, sound included when the microphone was granted.
 * @param {number} videoBitsPerSecond
 * @param {number} audioBitsPerSecond
 * @returns {boolean} Whether recording started.
 */
window.rvs_cameraStartRecording = function (videoBitsPerSecond, audioBitsPerSecond) {
    if (!_rvs_camera.stream || typeof MediaRecorder === 'undefined') return false;

    var type = _rvs_negotiateVideoType();
    var options = { videoBitsPerSecond: videoBitsPerSecond, audioBitsPerSecond: audioBitsPerSecond };
    if (type) options.mimeType = type;

    var recorder;
    try {
        recorder = new MediaRecorder(_rvs_camera.stream, options);
    } catch (e) {
        try { recorder = new MediaRecorder(_rvs_camera.stream); } catch (e2) { return false; }
    }

    _rvs_camera.chunks = [];
    recorder.ondataavailable = function (event) {
        if (event.data && event.data.size > 0) _rvs_camera.chunks.push(event.data);
    };
    recorder.start(1000);
    _rvs_camera.recorder = recorder;
    _rvs_camera.recordingType = recorder.mimeType || type;
    return true;
};

/**
 * Stops recording and returns the clip. The camera keeps running for the next capture.
 * @returns {Promise<Uint8Array|null>}
 */
window.rvs_cameraStopRecording = function () {
    return new Promise(function (resolve) {
        var recorder = _rvs_camera.recorder;
        if (!recorder || recorder.state === 'inactive') {
            resolve(null);
            return;
        }

        recorder.onstop = async function () {
            var blob = new Blob(_rvs_camera.chunks, { type: _rvs_camera.recordingType || 'video/webm' });
            _rvs_camera.chunks = [];
            _rvs_camera.recorder = null;
            resolve(blob.size > 0 ? new Uint8Array(await blob.arrayBuffer()) : null);
        };
        recorder.stop();
    });
};

/**
 * The MIME type of the last recording, codec parameters and all, as the recorder reported it.
 * @returns {string}
 */
window.rvs_cameraRecordingType = function () {
    return _rvs_camera.recordingType;
};

/**
 * Discards any recording in progress and releases the camera and microphone.
 */
window.rvs_cameraStop = function () {
    var recorder = _rvs_camera.recorder;
    if (recorder && recorder.state !== 'inactive') {
        recorder.ondataavailable = null;
        recorder.onstop = null;
        try { recorder.stop(); } catch (e) { /* already stopping */ }
    }
    _rvs_camera.recorder = null;
    _rvs_camera.chunks = [];
    _rvs_camera.lastPhoto = null;

    if (_rvs_camera.stream) {
        _rvs_camera.stream.getTracks().forEach(function (track) { track.stop(); });
        _rvs_camera.stream = null;
    }
};
