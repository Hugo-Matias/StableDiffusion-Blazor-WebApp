let scrollHandler = null;
let resizeHandler = null;
let resizeTimeout = null;
let mutationObserver = null;
let lastItemCount = 0;
let positionedItems = new Set(); // Track which items have been positioned

const COLUMN_CONFIG = {
    xxl: { breakpoint: 1600, columns: 5 },
    xl: { breakpoint: 1200, columns: 4 },
    lg: { breakpoint: 768, columns: 3 },
    md: { breakpoint: 480, columns: 2 },
    sm: { breakpoint: 0, columns: 1 }
};

const GAP = 16; // 1rem in pixels

export function attachWindowScroll(dotnetRef, options) {
    // Increased threshold from 500px to 1500px for earlier loading
    const threshold = options?.threshold ?? 2000;
    let isLoading = false;

    scrollHandler = function () {
        if (isLoading) return;

        const scrollPos = window.scrollY + window.innerHeight;
        const bottom = document.documentElement.scrollHeight;

        if (bottom - scrollPos < threshold) {
            isLoading = true;
            dotnetRef.invokeMethodAsync('LoadNextPage')
                .then(() => {
                    isLoading = false;
                    // Reduced timeout from 150ms to 100ms for faster response
                    setTimeout(() => layoutMasonry(), 100);
                })
                .catch((error) => {
                    console.error('Error loading next page:', error);
                    isLoading = false;
                });
        }
    };

    resizeHandler = function () {
        if (resizeTimeout) clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(() => {
            // Clear positioned items tracking on resize (full relayout)
            positionedItems.clear();
            layoutMasonry();
        }, 250);
    };

    // Watch for DOM changes to detect when items are added or removed
    const container = document.querySelector('.gallery-flex');
    if (container) {
        mutationObserver = new MutationObserver((mutations) => {
            const currentItemCount = container.querySelectorAll('.gallery-item').length;

            // Items were removed (project change)
            if (currentItemCount < lastItemCount) {
                console.log(`Items removed: ${lastItemCount} → ${currentItemCount}, resetting container`);
                container.style.height = '0px'; // Reset height immediately
                lastItemCount = currentItemCount;
                positionedItems.clear(); // Clear tracking

                // If there are still items, layout them
                if (currentItemCount > 0) {
                    setTimeout(() => layoutMasonry(), 100);
                }
            }
            // Items were added
            else if (currentItemCount > lastItemCount) {
                lastItemCount = currentItemCount;
                // New items added, layout will handle them
            }
        });

        mutationObserver.observe(container, {
            childList: true,
            subtree: false
        });

        // Initialize count
        lastItemCount = container.querySelectorAll('.gallery-item').length;
    }

    window.addEventListener('scroll', scrollHandler, { passive: true });
    window.addEventListener('resize', resizeHandler);

    // Initial layout - wait for initial render
    setTimeout(() => layoutMasonry(), 200);

    return {
        dispose() {
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
            lastItemCount = 0;
            positionedItems.clear();
        }
    };
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
    const container = document.querySelector('.gallery-flex');
    if (!container) {
        console.warn('Gallery container not found');
        return;
    }

    const items = container.querySelectorAll('.gallery-item');
    if (items.length === 0) {
        console.log('No items to layout');
        container.style.height = '0px'; // Ensure container is empty when no items
        return;
    }

    // Immediately hide all unpositioned items to prevent flashing
    items.forEach((item, index) => {
        if (!positionedItems.has(index)) {
            item.style.opacity = '0';
            item.style.visibility = 'hidden';
        }
    });

    // Wait for all images to load
    await waitForImages(container);

    const columnCount = getColumnCount();
    const containerWidth = container.offsetWidth;
    const itemWidth = (containerWidth - (GAP * (columnCount - 1))) / columnCount;

    // Initialize column heights
    const columnHeights = new Array(columnCount).fill(0);

    console.log(`Laying out ${items.length} items in ${columnCount} columns`);

    // Layout each item
    items.forEach((item, index) => {
        // Find the shortest column
        let shortestColumn = 0;
        let minHeight = columnHeights[0];

        for (let i = 1; i < columnCount; i++) {
            if (columnHeights[i] < minHeight) {
                minHeight = columnHeights[i];
                shortestColumn = i;
            }
        }

        // Calculate position
        const x = shortestColumn * (itemWidth + GAP);
        const y = columnHeights[shortestColumn];

        // Apply positioning
        item.style.width = `${itemWidth}px`;
        item.style.left = `${x}px`;
        item.style.top = `${y}px`;

        // Show the item with a smooth fade-in
        item.style.visibility = 'visible';
        item.style.opacity = '1';

        // Mark as positioned
        positionedItems.add(index);

        // Update column height
        const itemHeight = item.offsetHeight;
        columnHeights[shortestColumn] += itemHeight + GAP;

        if (index < 5) {
            console.log(`Item ${index}: column ${shortestColumn}, x=${x.toFixed(0)}, y=${y.toFixed(0)}, height=${itemHeight}`);
        }
    });

    // Set container height to tallest column
    const maxHeight = Math.max(...columnHeights);
    container.style.height = `${maxHeight}px`;

    console.log(`Container height: ${maxHeight}px, Column heights:`, columnHeights.map(h => Math.round(h)));
}

function waitForImages(container) {
    return new Promise((resolve) => {
        const images = container.querySelectorAll('img');

        if (images.length === 0) {
            resolve();
            return;
        }

        let loadedCount = 0;
        let totalImages = images.length;

        const checkComplete = () => {
            loadedCount++;
            if (loadedCount >= totalImages) {
                // Give a small delay for final rendering
                setTimeout(resolve, 50);
            }
        };

        images.forEach(img => {
            if (img.complete && img.naturalHeight !== 0) {
                checkComplete();
            } else {
                img.addEventListener('load', checkComplete, { once: true });
                img.addEventListener('error', checkComplete, { once: true });
            }
        });

        // Fallback timeout in case some images never trigger load/error
        setTimeout(() => {
            if (loadedCount < totalImages) {
                console.warn(`Only ${loadedCount}/${totalImages} images loaded, proceeding anyway`);
                resolve();
            }
        }, 3000);
    });
}