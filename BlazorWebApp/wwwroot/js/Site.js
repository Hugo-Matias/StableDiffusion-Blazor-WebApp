window.clipboardCopy = {
  copyText: function (text) {
    navigator.clipboard.writeText(text).catch(function (error) {
      alert(error);
    });
  },
};

window.workshopChat = {
  scrollToBottom: function (element, smooth) {
    if (!element) return;
    try {
      element.scrollTo({
        top: element.scrollHeight,
        behavior: smooth ? "smooth" : "auto",
      });
    } catch (e) {
      element.scrollTop = element.scrollHeight;
    }
  },
};

window.downloadFile = function (filename, content, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
};

window.civitaiModelNavigation = {
  getScrollTop: function (container) {
    if (!container)
      return window.scrollY || document.documentElement.scrollTop || 0;
    return container.scrollTop || 0;
  },
  watchScroll: function (container, storageKey) {
    if (!container || !storageKey) return;
    if (container.__civitaiScrollWatcherKey === storageKey) return;
    if (container.__civitaiScrollWatcher) {
      container.removeEventListener("scroll", container.__civitaiScrollWatcher);
    }

    const watcher = function () {
      try {
        sessionStorage.setItem(storageKey, String(container.scrollTop || 0));
      } catch (e) {}
    };

    container.__civitaiScrollWatcher = watcher;
    container.__civitaiScrollWatcherKey = storageKey;
    container.addEventListener("scroll", watcher, { passive: true });
  },
  restoreResults: function (
    container,
    anchorId,
    fallbackScrollTop,
    storageKey,
  ) {
    requestAnimationFrame(function () {
      const anchor = anchorId ? document.getElementById(anchorId) : null;
      if (anchor) {
        anchor.scrollIntoView({
          block: "center",
          inline: "nearest",
          behavior: "auto",
        });
        return;
      }

      let storedScrollTop = null;
      if (storageKey) {
        try {
          const storedValue = sessionStorage.getItem(storageKey);
          storedScrollTop = storedValue === null ? null : Number(storedValue);
        } catch (e) {
          storedScrollTop = null;
        }
      }

      const scrollTop =
        typeof storedScrollTop === "number" && !Number.isNaN(storedScrollTop)
          ? storedScrollTop
          : typeof fallbackScrollTop === "number"
            ? fallbackScrollTop
            : 0;

      if (container) {
        container.scrollTop = scrollTop;
        return;
      }

      window.scrollTo({
        top: scrollTop,
        behavior: "auto",
      });
    });
  },
};

window.assetViewerPanZoom = (function () {
  let stageEl = null;
  let mediaEl = null;
  let indicatorEl = null;
  let dragging = false;
  let pointerId = null;
  let panX = 0;
  let panY = 0;
  let zoom = 1;
  let startX = 0;
  let startY = 0;
  let startPanX = 0;
  let startPanY = 0;
  let minZoom = 0.25;
  let maxZoom = 5;
  let zoomStep = 0.15;
  let animationFrame = 0;
  let indicatorTimer = 0;
  let onPointerDown = null;
  let onPointerMove = null;
  let onPointerUp = null;
  let onWheel = null;
  const INDICATOR_HIDE_DELAY = 800;

  function clampZoom(value) {
    return Math.max(minZoom, Math.min(maxZoom, value));
  }

  function applyTransform() {
    if (!mediaEl) return;
    mediaEl.style.setProperty("--asset-pan-x", panX.toFixed(3) + "px");
    mediaEl.style.setProperty("--asset-pan-y", panY.toFixed(3) + "px");
    mediaEl.style.setProperty("--asset-zoom", zoom.toFixed(4));
  }

  function scheduleTransform() {
    if (animationFrame) return;
    animationFrame = requestAnimationFrame(function () {
      animationFrame = 0;
      applyTransform();
    });
  }

  function setDragging(value) {
    dragging = !!value;
    if (stageEl) stageEl.classList.toggle("dragging", dragging);
    if (mediaEl) mediaEl.classList.toggle("dragging", dragging);
  }

  function showIndicator() {
    if (!indicatorEl) return;
    indicatorEl.textContent = Math.round(zoom * 100) + "%";
    indicatorEl.classList.add("visible");
    if (indicatorTimer) clearTimeout(indicatorTimer);
    indicatorTimer = setTimeout(function () {
      if (indicatorEl) indicatorEl.classList.remove("visible");
      indicatorTimer = 0;
    }, INDICATOR_HIDE_DELAY);
  }

  function setZoom(value, show) {
    const nextZoom = clampZoom(value);
    if (nextZoom === zoom) return;
    zoom = nextZoom;
    scheduleTransform();
    if (show) showIndicator();
  }

  function resetState(show) {
    panX = 0;
    panY = 0;
    zoom = 1;
    scheduleTransform();
    if (show) showIndicator();
  }

  function releasePointer(e) {
    if (!stageEl || pointerId === null) return;
    try {
      stageEl.releasePointerCapture(pointerId);
    } catch (error) {
      // Pointer capture may already be released by the browser.
    }
    pointerId = null;
  }

  return {
    attach: function (stage, media, indicator, min, max, step) {
      window.assetViewerPanZoom.detach();
      if (!stage || !media) return;

      stageEl = stage;
      mediaEl = media;
      indicatorEl = indicator || null;
      minZoom = typeof min === "number" ? min : minZoom;
      maxZoom = typeof max === "number" ? max : maxZoom;
      zoomStep = typeof step === "number" ? step : zoomStep;
      resetState(false);
      applyTransform();

      onPointerDown = function (e) {
        if (e.button !== 0 || !stageEl || !mediaEl) return;
        pointerId = e.pointerId;
        startX = e.clientX;
        startY = e.clientY;
        startPanX = panX;
        startPanY = panY;
        setDragging(true);
        try {
          stageEl.setPointerCapture(pointerId);
        } catch (error) {
          // Pointer capture is an optimization, not a requirement.
        }
        e.preventDefault();
      };

      onPointerMove = function (e) {
        if (!dragging) return;
        if (pointerId !== null && e.pointerId !== pointerId) return;
        panX = startPanX + e.clientX - startX;
        panY = startPanY + e.clientY - startY;
        scheduleTransform();
        e.preventDefault();
      };

      onPointerUp = function (e) {
        if (!dragging) return;
        if (pointerId !== null && e.pointerId !== pointerId) return;
        releasePointer(e);
        setDragging(false);
      };

      onWheel = function (e) {
        e.preventDefault();
        setZoom(zoom + (e.deltaY > 0 ? -zoomStep : zoomStep), true);
      };

      stageEl.addEventListener("pointerdown", onPointerDown);
      stageEl.addEventListener("pointermove", onPointerMove);
      stageEl.addEventListener("pointerup", onPointerUp);
      stageEl.addEventListener("pointercancel", onPointerUp);
      stageEl.addEventListener("wheel", onWheel, { passive: false });
    },
    detach: function () {
      if (stageEl && onPointerDown)
        stageEl.removeEventListener("pointerdown", onPointerDown);
      if (stageEl && onPointerMove)
        stageEl.removeEventListener("pointermove", onPointerMove);
      if (stageEl && onPointerUp) {
        stageEl.removeEventListener("pointerup", onPointerUp);
        stageEl.removeEventListener("pointercancel", onPointerUp);
      }
      if (stageEl && onWheel) stageEl.removeEventListener("wheel", onWheel);
      if (animationFrame) cancelAnimationFrame(animationFrame);
      if (indicatorTimer) clearTimeout(indicatorTimer);
      if (indicatorEl) indicatorEl.classList.remove("visible");
      if (stageEl) stageEl.classList.remove("dragging");
      if (mediaEl) {
        mediaEl.classList.remove("dragging");
        mediaEl.style.removeProperty("--asset-pan-x");
        mediaEl.style.removeProperty("--asset-pan-y");
        mediaEl.style.removeProperty("--asset-zoom");
      }
      stageEl = null;
      mediaEl = null;
      indicatorEl = null;
      dragging = false;
      pointerId = null;
      panX = 0;
      panY = 0;
      zoom = 1;
      animationFrame = 0;
      indicatorTimer = 0;
      onPointerDown = onPointerMove = onPointerUp = onWheel = null;
    },
    reset: function () {
      resetState(true);
    },
    zoomBy: function (delta) {
      setZoom(zoom + (typeof delta === "number" ? delta : 0), true);
    },
  };
})();

window.assetViewerCompare = (function () {
  let stageEl = null;
  let frameEl = null;
  let locked = false;
  let dragging = false;
  let split = 0.5;
  let onMove = null;
  let onDown = null;
  let onUp = null;
  let onResize = null;
  let onImageLoad = null;
  let mediaEl = null;
  let pointerActive = false;
  let handleTimer = 0;
  const HANDLE_HIDE_DELAY = 1800;

  function setActiveAreaVisible(value) {
    pointerActive = !!value;
  }

  function showHandle() {
    if (!frameEl) return;
    frameEl.classList.add("handle-visible");
    if (handleTimer) clearTimeout(handleTimer);
    handleTimer = setTimeout(function () {
      if (frameEl) frameEl.classList.remove("handle-visible");
      handleTimer = 0;
    }, HANDLE_HIDE_DELAY);
  }

  function hideHandleNow() {
    if (handleTimer) {
      clearTimeout(handleTimer);
      handleTimer = 0;
    }
    if (frameEl) frameEl.classList.remove("handle-visible");
  }

  function applySplit(v) {
    if (typeof v !== "number" || isNaN(v)) return;
    split = Math.max(0, Math.min(1, v));
    if (frameEl) {
      frameEl.style.setProperty("--ab-split", (split * 100).toFixed(3) + "%");
    }
  }

  function updateFrameBounds() {
    if (!stageEl || !frameEl) return null;
    if (!mediaEl || !stageEl.contains(mediaEl)) {
      mediaEl = stageEl.querySelector("img.viewer-media");
      if (mediaEl && onImageLoad) mediaEl.addEventListener("load", onImageLoad);
    }
    if (!mediaEl) return null;

    const stageRect = stageEl.getBoundingClientRect();
    const mediaRect = mediaEl.getBoundingClientRect();
    if (
      stageRect.width <= 0 ||
      stageRect.height <= 0 ||
      mediaRect.width <= 0 ||
      mediaRect.height <= 0
    )
      return null;

    frameEl.style.left = mediaRect.left - stageRect.left + "px";
    frameEl.style.top = mediaRect.top - stageRect.top + "px";
    frameEl.style.width = mediaRect.width + "px";
    frameEl.style.height = mediaRect.height + "px";
    return mediaRect;
  }

  function pointerInsideActiveArea(e) {
    const r = updateFrameBounds();
    if (!r) return false;
    if (r.width <= 0 || r.height <= 0) return false;
    return (
      e.clientX >= r.left &&
      e.clientX <= r.right &&
      e.clientY >= r.top &&
      e.clientY <= r.bottom
    );
  }

  function computeFromEvent(e) {
    if (!frameEl) return null;
    const r = updateFrameBounds();
    if (!r) return null;
    if (r.width <= 0) return null;
    return (e.clientX - r.left) / r.width;
  }

  return {
    attach: function (stage, frame, initialSplit) {
      // Always detach prior listeners (using the module ref, not `this`, to be safe
      // against unusual JS interop dispatch contexts).
      window.assetViewerCompare.detach();
      stageEl = stage;
      frameEl = frame;
      locked = false;
      dragging = false;
      pointerActive = false;
      onImageLoad = function () {
        updateFrameBounds();
      };
      onResize = function () {
        updateFrameBounds();
      };
      mediaEl = stageEl ? stageEl.querySelector("img.viewer-media") : null;
      if (mediaEl) mediaEl.addEventListener("load", onImageLoad);
      updateFrameBounds();
      setActiveAreaVisible(false);
      applySplit(typeof initialSplit === "number" ? initialSplit : 0.5);

      onMove = function (e) {
        if (!stageEl) return;
        const inside = pointerInsideActiveArea(e);
        setActiveAreaVisible(inside);
        // Handle is auto-hidden; only reveal it while locked AND the cursor is
        // moving over the image. A timer hides it again after a short idle.
        if (locked && inside) {
          showHandle();
        }
        // While dragging the handle we always track. Otherwise: skip when locked,
        // and only act when the cursor is over the image rect (so toolbar/info-panel
        // hovers don't snap the slider).
        if (!dragging) {
          if (locked) return;
          if (!inside) return;
        }
        const v = computeFromEvent(e);
        if (v !== null) applySplit(v);
      };
      onDown = function (e) {
        if (e.button !== 0 || !stageEl) return;
        const onHandle =
          e.target && e.target.closest
            ? e.target.closest(".compare-divider, .compare-handle")
            : null;
        if (onHandle) {
          dragging = true;
          const v = computeFromEvent(e);
          if (v !== null) applySplit(v);
          // Don't stop propagation here - Blazor's stage @onmousedown is fine to run
          // (it only acts in pan mode). HandleMouseUp checks wasDragging() to
          // suppress the synthetic click that toggles lock.
        }
      };
      onUp = function () {
        // Defer clearing so Blazor's HandleMouseUp can read wasDragging() first.
        if (dragging) {
          setTimeout(function () {
            dragging = false;
            setActiveAreaVisible(pointerActive);
          }, 50);
        }
      };

      // Listen at document level so events from any descendant (image, overlay,
      // divider) reach us regardless of Blazor's element diffing or stopPropagation
      // calls higher up the tree.
      document.addEventListener("mousemove", onMove, true);
      document.addEventListener("mousedown", onDown, true);
      document.addEventListener("mouseup", onUp, true);
      window.addEventListener("resize", onResize);
    },
    detach: function () {
      if (onMove) document.removeEventListener("mousemove", onMove, true);
      if (onDown) document.removeEventListener("mousedown", onDown, true);
      if (onUp) document.removeEventListener("mouseup", onUp, true);
      if (onResize) window.removeEventListener("resize", onResize);
      if (mediaEl && onImageLoad)
        mediaEl.removeEventListener("load", onImageLoad);
      hideHandleNow();
      stageEl = null;
      frameEl = null;
      mediaEl = null;
      onMove = onDown = onUp = onResize = onImageLoad = null;
      locked = false;
      dragging = false;
      pointerActive = false;
    },
    setLocked: function (value) {
      locked = !!value;
      setActiveAreaVisible(pointerActive);
      if (!locked) hideHandleNow();
    },
    setSplit: function (value) {
      applySplit(value);
    },
    getSplit: function () {
      return split;
    },
    wasDragging: function () {
      return dragging;
    },
    isPointInsideActiveArea: function (clientX, clientY) {
      const inside = pointerInsideActiveArea({
        clientX: clientX,
        clientY: clientY,
      });
      setActiveAreaVisible(inside);
      return inside;
    },
  };
})();
