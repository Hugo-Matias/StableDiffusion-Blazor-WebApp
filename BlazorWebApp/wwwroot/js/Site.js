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

window.assetViewerCompare = (function () {
  let stageEl = null;
  let frameEl = null;
  let locked = false;
  let dragging = false;
  let split = 0.5;
  let onMove = null;
  let onDown = null;
  let onUp = null;
  let handleTimer = 0;
  const HANDLE_HIDE_DELAY = 1800;

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

  function pointerInsideStage(e) {
    if (!stageEl) return false;
    const r = stageEl.getBoundingClientRect();
    if (r.width <= 0 || r.height <= 0) return false;
    return (
      e.clientX >= r.left &&
      e.clientX <= r.right &&
      e.clientY >= r.top &&
      e.clientY <= r.bottom
    );
  }

  function computeFromEvent(e) {
    if (!stageEl) return null;
    const r = stageEl.getBoundingClientRect();
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
      applySplit(typeof initialSplit === "number" ? initialSplit : 0.5);

      onMove = function (e) {
        if (!stageEl) return;
        const inside = pointerInsideStage(e);
        // Handle is auto-hidden; only reveal it while locked AND the cursor is
        // moving over the stage. A timer hides it again after a short idle.
        if (locked && inside) {
          showHandle();
        }
        // While dragging the handle we always track. Otherwise: skip when locked,
        // and only act when the cursor is over the stage rect (so toolbar/info-panel
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
          }, 50);
        }
      };

      // Listen at document level so events from any descendant (image, overlay,
      // divider) reach us regardless of Blazor's element diffing or stopPropagation
      // calls higher up the tree.
      document.addEventListener("mousemove", onMove, true);
      document.addEventListener("mousedown", onDown, true);
      document.addEventListener("mouseup", onUp, true);
    },
    detach: function () {
      if (onMove) document.removeEventListener("mousemove", onMove, true);
      if (onDown) document.removeEventListener("mousedown", onDown, true);
      if (onUp) document.removeEventListener("mouseup", onUp, true);
      hideHandleNow();
      stageEl = null;
      frameEl = null;
      onMove = onDown = onUp = null;
      locked = false;
      dragging = false;
    },
    setLocked: function (value) {
      locked = !!value;
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
  };
})();
