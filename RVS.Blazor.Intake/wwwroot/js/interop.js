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

/**
 * Starts recording audio from the user's microphone.
 * Requests microphone permission and begins capturing audio in webm format.
 * @returns {Promise<void>}
 */
window.rvs_startRecording = async function () {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    window._rvs_audioChunks = [];
    window._rvs_mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });

    window._rvs_mediaRecorder.ondataavailable = function (event) {
        if (event.data.size > 0) {
            window._rvs_audioChunks.push(event.data);
        }
    };

    window._rvs_mediaRecorder.start();
};

/**
 * Stops recording and returns the captured audio as a base64-encoded string.
 * Releases the microphone after stopping.
 * @returns {Promise<string|null>} Base64-encoded audio data, or null if no audio was captured.
 */
window.rvs_stopRecording = function () {
    return new Promise(function (resolve) {
        if (!window._rvs_mediaRecorder || window._rvs_mediaRecorder.state === 'inactive') {
            resolve(null);
            return;
        }

        window._rvs_mediaRecorder.onstop = function () {
            const blob = new Blob(window._rvs_audioChunks, { type: 'audio/webm' });
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
                resolve(base64 || null);
            };
            reader.readAsDataURL(blob);
        };

        window._rvs_mediaRecorder.stop();
    });
};
