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
