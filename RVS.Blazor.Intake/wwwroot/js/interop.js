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
