// Infinite-scroll helper for the Danbooru Search tab.
// The visual masonry itself is now pure CSS (column-count) in
// Danbooru.razor.css, so this module no longer measures or positions
// anything. It only watches the window scroll position and calls back
// into .NET when the user nears the bottom of the page.

let scrollHandler = null;
let dotnetReference = null;
let isLoadingMore = false;
let hasMoreContent = true;
let isDisposed = false;

export function attachWindowScroll(dotnetRef, options) {
    // Clean up any previous instance first.
    dispose();

    isDisposed = false;
    hasMoreContent = true;
    isLoadingMore = false;
    dotnetReference = dotnetRef;

    const threshold = options?.threshold ?? 1500;

    scrollHandler = function () {
        if (isDisposed || isLoadingMore || !hasMoreContent || !dotnetReference) return;

        const scrollPos = window.scrollY + window.innerHeight;
        const bottom = document.documentElement.scrollHeight;
        const distanceFromBottom = bottom - scrollPos;

        if (distanceFromBottom < threshold) {
            loadMore();
        }
    };

    window.addEventListener('scroll', scrollHandler, { passive: true });

    return { dispose };
}

async function loadMore() {
    if (isDisposed || isLoadingMore || !hasMoreContent || !dotnetReference) return;

    isLoadingMore = true;
    try {
        await dotnetReference.invokeMethodAsync('LoadNextPage');
    } catch (error) {
        // Silently handle disposed/disconnected errors.
        hasMoreContent = false;
        isDisposed = true;
    } finally {
        isLoadingMore = false;
    }
}

export function setHasMore(value) {
    if (!isDisposed) {
        hasMoreContent = value;
    }
}

// Kept as no-ops for backwards compatibility with existing Danbooru.razor
// call sites; the CSS column layout needs no explicit reset.
export function resetLayout() { /* no-op: CSS handles layout */ }
export function layoutOnce() { /* no-op: CSS handles layout */ }

export function dispose() {
    isDisposed = true;
    dotnetReference = null;

    if (scrollHandler) {
        window.removeEventListener('scroll', scrollHandler);
        scrollHandler = null;
    }

    isLoadingMore = false;
    hasMoreContent = true;
}
