let scrollHandler = null;
let resizeHandler = null;
let resizeTimeout = null;
let mutationObserver = null;
let lastItemCount = 0;
let positionedItems = new Set();
let dotnetReference = null;
let isLoadingMore = false;
let hasMoreContent = true;
let isDisposed = false;
let layoutInProgress = false;

const COLUMN_CONFIG = {
    xxl: { breakpoint: 1600, columns: 5 },
    xl: { breakpoint: 1200, columns: 4 },
    lg: { breakpoint: 768, columns: 3 },
    md: { breakpoint: 480, columns: 2 },
    sm: { breakpoint: 0, columns: 1 }
};

const GAP = 16;

export function attachWindowScroll(dotnetRef, options) {
    // Clean up any previous instance first
    cleanupHandlers();
    
    // Reset state
    isDisposed = false;
    hasMoreContent = true;
    isLoadingMore = false;
    layoutInProgress = false;
    positionedItems.clear();
    lastItemCount = 0;
    
    dotnetReference = dotnetRef;
    const threshold = options?.threshold ?? 1500;

    scrollHandler = function () {
        if (isDisposed || isLoadingMore || !hasMoreContent || !dotnetReference || layoutInProgress) return;

        const scrollPos = window.scrollY + window.innerHeight;
        const bottom = document.documentElement.scrollHeight;
        const distanceFromBottom = bottom - scrollPos;

        if (distanceFromBottom < threshold) {
            loadMore();
        }
    };

    resizeHandler = function () {
        if (isDisposed) return;
        if (resizeTimeout) clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(() => {
            positionedItems.clear();
            layoutMasonry();
        }, 250);
    };

    const container = document.querySelector('.danbooru-masonry');
    if (container) {
        mutationObserver = new MutationObserver((mutations) => {
            if (isDisposed || layoutInProgress) return;
            
            const currentItemCount = container.querySelectorAll('.danbooru-item').length;

            if (currentItemCount !== lastItemCount) {
                if (currentItemCount < lastItemCount) {
                    // Items were removed (new search)
                    container.style.height = '0px';
                    positionedItems.clear();
                    hasMoreContent = true;
                }
                
                lastItemCount = currentItemCount;
                
                if (currentItemCount > 0) {
                    // Debounce layout calls
                    if (resizeTimeout) clearTimeout(resizeTimeout);
                    resizeTimeout = setTimeout(() => layoutMasonry(), 150);
                }
            }
        });

        mutationObserver.observe(container, {
            childList: true,
            subtree: false
        });

        lastItemCount = container.querySelectorAll('.danbooru-item').length;
    }

    window.addEventListener('scroll', scrollHandler, { passive: true });
    window.addEventListener('resize', resizeHandler);

    // Initial layout with a delay to ensure images start loading
    setTimeout(() => layoutMasonry(), 300);

    return { dispose };
}

function cleanupHandlers() {
    if (scrollHandler) {
        window.removeEventListener('scroll', scrollHandler);
        scrollHandler = null;
    }
    if (resizeHandler) {
        window.removeEventListener('resize', resizeHandler);
        resizeHandler = null;
    }
    if (resizeTimeout) {
        clearTimeout(resizeTimeout);
        resizeTimeout = null;
    }
    if (mutationObserver) {
        mutationObserver.disconnect();
        mutationObserver = null;
    }
}

async function loadMore() {
    if (isDisposed || isLoadingMore || !hasMoreContent || !dotnetReference) return;
    
    isLoadingMore = true;
    
    try {
        await dotnetReference.invokeMethodAsync('LoadNextPage');
    } catch (error) {
        // Silently handle disposed/disconnected errors
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

export function resetLayout() {
    if (isDisposed) return;
    
    positionedItems.clear();
    lastItemCount = 0;
    isLoadingMore = false;
    hasMoreContent = true;
    layoutInProgress = false;
    
    const container = document.querySelector('.danbooru-masonry');
    if (container) {
        container.style.height = '0px';
        const items = container.querySelectorAll('.danbooru-item');
        items.forEach(item => {
            item.style.opacity = '0';
            item.style.visibility = 'hidden';
        });
        lastItemCount = items.length;
    }
    setTimeout(() => layoutMasonry(), 200);
}

export function dispose() {
    isDisposed = true;
    dotnetReference = null;
    
    cleanupHandlers();
    
    lastItemCount = 0;
    positionedItems.clear();
    isLoadingMore = false;
    hasMoreContent = true;
    layoutInProgress = false;
}

function getColumnCount() {
    const width = window.innerWidth;
    for (const key in COLUMN_CONFIG) {
        if (width > COLUMN_CONFIG[key].breakpoint) {
            return COLUMN_CONFIG[key].columns;
        }
    }
    return 1;
}

async function layoutMasonry() {
    if (isDisposed || layoutInProgress) return;
    
    layoutInProgress = true;
    
    try {
        const container = document.querySelector('.danbooru-masonry');
        if (!container) {
            layoutInProgress = false;
            return;
        }

        const items = container.querySelectorAll('.danbooru-item');
        if (items.length === 0) {
            container.style.height = '0px';
            layoutInProgress = false;
            return;
        }

        // Hide only new unpositioned items
        items.forEach((item, index) => {
            if (!positionedItems.has(index)) {
                item.style.opacity = '0';
                item.style.visibility = 'hidden';
            }
        });

        // Wait for images to load
        await waitForImages(container);
        
        if (isDisposed) {
            layoutInProgress = false;
            return;
        }

        const columnCount = getColumnCount();
        const containerWidth = container.offsetWidth;
        
        if (containerWidth === 0) {
            // Container not visible yet, retry
            layoutInProgress = false;
            setTimeout(() => layoutMasonry(), 100);
            return;
        }
        
        const itemWidth = (containerWidth - (GAP * (columnCount - 1))) / columnCount;
        const columnHeights = new Array(columnCount).fill(0);

        items.forEach((item, index) => {
            let shortestColumn = 0;
            let minHeight = columnHeights[0];

            for (let i = 1; i < columnCount; i++) {
                if (columnHeights[i] < minHeight) {
                    minHeight = columnHeights[i];
                    shortestColumn = i;
                }
            }

            const x = shortestColumn * (itemWidth + GAP);
            const y = columnHeights[shortestColumn];

            item.style.width = `${itemWidth}px`;
            item.style.left = `${x}px`;
            item.style.top = `${y}px`;

            // Show the item
            item.style.visibility = 'visible';
            item.style.opacity = '1';

            positionedItems.add(index);

            const itemHeight = item.offsetHeight;
            columnHeights[shortestColumn] += itemHeight + GAP;
        });

        const maxHeight = Math.max(...columnHeights);
        container.style.height = `${maxHeight}px`;
        
    } finally {
        layoutInProgress = false;
    }
}

function waitForImages(container) {
    return new Promise((resolve) => {
        // Only wait for images that haven't been loaded yet (new images)
        const images = Array.from(container.querySelectorAll('.danbooru-item img'));
        const unloadedImages = images.filter(img => !img.complete || img.naturalHeight === 0);

        if (unloadedImages.length === 0) {
            resolve();
            return;
        }

        let loadedCount = 0;
        const totalToLoad = unloadedImages.length;

        const checkComplete = () => {
            loadedCount++;
            if (loadedCount >= totalToLoad) {
                setTimeout(resolve, 50);
            }
        };

        unloadedImages.forEach(img => {
            img.addEventListener('load', checkComplete, { once: true });
            img.addEventListener('error', checkComplete, { once: true });
        });

        // Shorter timeout for faster response
        setTimeout(() => {
            if (loadedCount < totalToLoad) {
                resolve();
            }
        }, 3000);
    });
}
