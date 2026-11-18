let scrollHandler = null;
let resizeHandler = null;
let resizeTimeout = null;
let mutationObserver = null;
let lastItemCount = 0;
let positionedItems = new Set(); // Track which items have been positioned
let layoutTimeout = null;

const COLUMN_CONFIG = {
  xxl: { breakpoint: 1600, columns: 5 },
  xl: { breakpoint: 1200, columns: 4 },
  lg: { breakpoint: 768, columns: 3 },
  md: { breakpoint: 480, columns: 2 },
  sm: { breakpoint: 0, columns: 1 },
};

const DEFAULT_GAP = 16;

export function attachWindowScroll(dotnetRef, options) {
  dispose();

  // Increased threshold from 500px to 1500px for earlier loading
  const threshold = options?.threshold ?? 2000;
  let isLoading = false;

  scrollHandler = function () {
    if (isLoading) return;

    const scrollPos = window.scrollY + window.innerHeight;
    const bottom = document.documentElement.scrollHeight;

    if (bottom - scrollPos < threshold) {
      isLoading = true;
      dotnetRef
        .invokeMethodAsync("LoadNextPage")
        .then(() => {
          isLoading = false;
          scheduleLayout(100);
        })
        .catch((error) => {
          console.error("Error loading next page:", error);
          isLoading = false;
        });
    }
  };

  resizeHandler = function () {
    if (resizeTimeout) clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
      // Clear positioned items tracking on resize (full relayout)
      positionedItems.clear();
      positionedItems.clear();
      scheduleLayout(0);
    }, 250);
  };

  // Watch for DOM changes to detect when items are added or removed
  const container = document.querySelector(".gallery-flex");
  if (container) {
    mutationObserver = new MutationObserver((mutations) => {
      const currentItemCount =
        container.querySelectorAll(".gallery-item").length;

      // Items were removed (project change)
      if (currentItemCount < lastItemCount) {
        console.log(
          `Items removed: ${lastItemCount} → ${currentItemCount}, resetting container`,
        );
        container.style.height = "0px"; // Reset height immediately
        lastItemCount = currentItemCount;
        positionedItems.clear(); // Clear tracking

        // If there are still items, layout them
        if (currentItemCount > 0) {
          scheduleLayout(100);
        }
      }
      // Items were added
      else if (currentItemCount > lastItemCount) {
        lastItemCount = currentItemCount;
        scheduleLayout(100);
      }
    });

    mutationObserver.observe(container, {
      childList: true,
      subtree: false,
    });

    // Initialize count
    lastItemCount = container.querySelectorAll(".gallery-item").length;
  }

  window.addEventListener("scroll", scrollHandler, { passive: true });
  window.addEventListener("resize", resizeHandler);

  // Initial layout - wait for initial render
  scheduleLayout(200);

  return {
    dispose,
  };
}

export function refreshLayout() {
  scheduleLayout(0);
}

export function dispose() {
  if (scrollHandler) {
    window.removeEventListener("scroll", scrollHandler);
    scrollHandler = null;
  }
  if (resizeHandler) {
    window.removeEventListener("resize", resizeHandler);
    resizeHandler = null;
  }
  if (resizeTimeout) {
    clearTimeout(resizeTimeout);
    resizeTimeout = null;
  }
  if (layoutTimeout) {
    clearTimeout(layoutTimeout);
    layoutTimeout = null;
  }
  if (mutationObserver) {
    mutationObserver.disconnect();
    mutationObserver = null;
  }
  lastItemCount = 0;
  positionedItems.clear();
}

function scheduleLayout(delay = 0) {
  if (layoutTimeout) clearTimeout(layoutTimeout);
  layoutTimeout = setTimeout(() => {
    layoutTimeout = null;
    layoutMasonry();
  }, delay);
}

function getColumnCount(container) {
  const width = window.innerWidth;
  const tileSize =
    container.closest(".masonry-gallery-wrapper")?.dataset?.tileSize ??
    "medium";

  for (const key in COLUMN_CONFIG) {
    if (width > COLUMN_CONFIG[key].breakpoint) {
      const baseColumns = COLUMN_CONFIG[key].columns;
      if (tileSize === "small") return Math.min(baseColumns + 1, 6);
      if (tileSize === "large") return Math.max(baseColumns - 1, 1);
      return baseColumns;
    }
  }
  return 1;
}

function getGap(container) {
  const wrapper = container.closest(".masonry-gallery-wrapper") ?? container;
  const rawGap = getComputedStyle(wrapper).getPropertyValue(
    "--gallery-masonry-gap",
  );
  const gap = Number.parseFloat(rawGap);
  return Number.isFinite(gap) ? gap : DEFAULT_GAP;
}

async function layoutMasonry() {
  const container = document.querySelector(".gallery-flex");
  if (!container) {
    console.warn("Gallery container not found");
    return;
  }

  const items = container.querySelectorAll(".gallery-item");
  if (items.length === 0) {
    console.log("No items to layout");
    container.style.height = "0px"; // Ensure container is empty when no items
    return;
  }

  // Immediately hide all unpositioned items to prevent flashing
  items.forEach((item, index) => {
    if (!positionedItems.has(index)) {
      item.style.opacity = "0";
      item.style.visibility = "hidden";
    }
  });

  // Wait for all images to load
  await waitForImages(container);

  const columnCount = getColumnCount(container);
  const gap = getGap(container);
  const containerWidth = container.offsetWidth;
  const itemWidth = (containerWidth - gap * (columnCount - 1)) / columnCount;

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
    const x = shortestColumn * (itemWidth + gap);
    const y = columnHeights[shortestColumn];

    // Apply positioning
    item.style.width = `${itemWidth}px`;
    item.style.left = `${x}px`;
    item.style.top = `${y}px`;

    // Show the item with a smooth fade-in
    item.style.visibility = "visible";
    item.style.opacity = "1";

    // Mark as positioned
    positionedItems.add(index);

    // Update column height
    const itemHeight = item.offsetHeight;
    columnHeights[shortestColumn] += itemHeight + gap;

    if (index < 5) {
      console.log(
        `Item ${index}: column ${shortestColumn}, x=${x.toFixed(0)}, y=${y.toFixed(0)}, height=${itemHeight}`,
      );
    }
  });

  // Set container height to tallest column
  const maxHeight = Math.max(...columnHeights);
  container.style.height = `${maxHeight}px`;

  console.log(
    `Container height: ${maxHeight}px, Column heights:`,
    columnHeights.map((h) => Math.round(h)),
  );
}

function waitForImages(container) {
  return new Promise((resolve) => {
    const images = container.querySelectorAll("img");
    const videos = container.querySelectorAll("video");

    const totalMedia = images.length + videos.length;
    if (totalMedia === 0) {
      resolve();
      return;
    }

    let loadedCount = 0;

    const checkComplete = () => {
      loadedCount++;
      if (loadedCount >= totalMedia) {
        // Give a small delay for final rendering
        setTimeout(resolve, 50);
      }
    };

    images.forEach((img) => {
      if (img.complete && img.naturalHeight !== 0) {
        checkComplete();
      } else {
        img.addEventListener("load", checkComplete, { once: true });
        img.addEventListener("error", checkComplete, { once: true });
      }
    });

    videos.forEach((video) => {
      if (video.readyState >= 1) {
        checkComplete();
      } else {
        video.addEventListener("loadedmetadata", checkComplete, { once: true });
        video.addEventListener("error", checkComplete, { once: true });
      }
    });

    // Fallback timeout in case some media never trigger load/error
    setTimeout(() => {
      if (loadedCount < totalMedia) {
        console.warn(
          `Only ${loadedCount}/${totalMedia} media items loaded, proceeding anyway`,
        );
        resolve();
      }
    }, 3000);
  });
}
