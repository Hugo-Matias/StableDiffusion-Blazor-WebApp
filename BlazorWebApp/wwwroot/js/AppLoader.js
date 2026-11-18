// AppLoader — controls the initial bootstrap overlay rendered in _Layout.cshtml.
// The overlay is dismissed explicitly by MainLayout via JS interop after the
// first render completes and state has been loaded. A safety timeout hides it
// regardless so the user can never get stuck on the loader.
(function () {
    const SAFETY_TIMEOUT_MS = 20000;
    let hidden = false;
    let removeTimer = null;

    function hide() {
        if (hidden) return;
        hidden = true;

        const el = document.getElementById('app-loader');
        if (!el) return;

        el.classList.add('is-hidden');

        // Remove from DOM after the fade-out transition to free memory and
        // release the `position: fixed` overlay.
        removeTimer = window.setTimeout(() => {
            if (el.parentNode) el.parentNode.removeChild(el);
        }, 700);
    }

    function setStatus(text) {
        const el = document.querySelector('#app-loader .status');
        if (el && typeof text === 'string') el.textContent = text;
    }

    // Safety net: if something goes wrong during Blazor startup, don't trap
    // the user behind an opaque overlay forever.
    window.setTimeout(hide, SAFETY_TIMEOUT_MS);

    window.appLoader = { hide: hide, setStatus: setStatus };
})();
