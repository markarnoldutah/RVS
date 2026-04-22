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
