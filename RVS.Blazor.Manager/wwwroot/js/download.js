/**
 * Downloads a base64-encoded file via a temporary anchor element.
 * @param {string} fileName - The name for the downloaded file.
 * @param {string} base64Data - The base64-encoded content.
 * @param {string} mimeType - The MIME type of the file.
 */
window.rvs_setHtmlOverflow = (value) => document.documentElement.style.overflow = value;

window.rvs_downloadBase64 = function (fileName, base64Data, mimeType) {
    const link = document.createElement("a");
    link.href = "data:" + mimeType + ";base64," + base64Data;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

/**
 * Opens a URL in a new tab and reports whether the browser allowed it. The Send intake link
 * dialog's "Fill it in myself" mints the invite first, so the call arrives after an await and
 * popup blockers may refuse it — the dialog then shows the link instead (Spec A-14, issue #666).
 * @param {string} url - The URL to open.
 * @returns {boolean} true when a window was opened.
 */
window.rvs_openInNewTab = function (url) {
    const opened = window.open(url, "_blank", "noopener");
    return opened !== null && opened !== undefined;
};
