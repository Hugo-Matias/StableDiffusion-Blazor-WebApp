// Helper functions for cursor position management
window.getCaretPosition = (element) => {
  try {
    if (!element) return 0;

    if (element.selectionStart !== undefined) {
      return element.selectionStart;
    }

    const selection = window.getSelection();
    if (selection.rangeCount > 0) {
      const range = selection.getRangeAt(0);
      const preCaretRange = range.cloneRange();
      preCaretRange.selectNodeContents(element);
      preCaretRange.setEnd(range.endContainer, range.endOffset);
      return preCaretRange.toString().length;
    }

    return 0;
  } catch (e) {
    console.error("Error getting caret position:", e);
    return 0;
  }
};

window.setCaretPosition = (element, position) => {
  try {
    if (!element) return;

    if (element.setSelectionRange !== undefined) {
      element.focus();
      element.setSelectionRange(position, position);
      return;
    }

    if (element.firstChild) {
      const range = document.createRange();
      const sel = window.getSelection();
      range.setStart(
        element.firstChild,
        Math.min(position, element.textContent.length),
      );
      range.collapse(true);
      sel.removeAllRanges();
      sel.addRange(range);
      element.focus();
    }
  } catch (e) {
    console.error("Error setting caret position:", e);
  }
};

// Store event handlers to avoid memory leaks
const autocompleteHandlers = new WeakMap();
const promptFieldsHandlers = new WeakMap();
const llmInstructionHandlers = new WeakMap();

/**
 * Initialize autocomplete functionality on a text input/textarea element
 * @param {HTMLElement} wrapperRef - The wrapper element containing the input
 * @param {Object} dotNetHelper - .NET object reference for interop callbacks
 */
export function initializeAutocomplete(wrapperRef, dotNetHelper) {
  if (!wrapperRef) {
    console.error("No element reference provided to initializeAutocomplete");
    return;
  }

  // Find the actual input/textarea element within the wrapper
  let element =
    wrapperRef.querySelector("input") || wrapperRef.querySelector("textarea");

  if (!element) {
    console.error("Could not find input or textarea element");
    return;
  }

  // Remove existing handlers if present to avoid duplicates
  if (autocompleteHandlers.has(element)) {
    const oldHandlers = autocompleteHandlers.get(element);
    element.removeEventListener("keydown", oldHandlers.keydown, true);
    element.removeEventListener("click", oldHandlers.click);
    element.removeEventListener("keyup", oldHandlers.keyup);
    if (oldHandlers.input) {
      element.removeEventListener("input", oldHandlers.input);
    }
  }

  // Keydown handler: intercepts navigation keys when dropdown is visible
  const keydownHandler = (e) => {
    const shouldHandle =
      e.key === "ArrowDown" ||
      e.key === "ArrowUp" ||
      e.key === "Enter" ||
      e.key === "Tab" ||
      e.key === "Escape";

    if (!shouldHandle) return;

    // Check dropdown visibility synchronously
    const dropdown = wrapperRef.querySelector(".autocomplete-dropdown");
    const isVisible = dropdown && dropdown.offsetParent !== null;

    if (isVisible) {
      // Prevent default behavior immediately before async operations
      e.preventDefault();
      e.stopPropagation();
      e.stopImmediatePropagation();

      // Handle the key asynchronously
      (async () => {
        try {
          await dotNetHelper.invokeMethodAsync("HandleKeyDownFromJS", e.key);
        } catch (error) {
          console.error("Error handling keydown in autocomplete:", error);
        }
      })();

      return false;
    }
  };

  // Click handler: updates cursor position in .NET
  const clickHandler = async (e) => {
    setTimeout(async () => {
      try {
        const position = element.selectionStart;
        await dotNetHelper.invokeMethodAsync(
          "OnCursorPositionChanged",
          position,
        );
      } catch (error) {
        console.error("Error handling click:", error);
      }
    }, 10);
  };

  // Keyup handler: updates cursor position on arrow key navigation and input changes
  const keyupHandler = async (e) => {
    // Track cursor position for navigation keys
    if (
      e.key === "ArrowLeft" ||
      e.key === "ArrowRight" ||
      e.key === "Home" ||
      e.key === "End"
    ) {
      setTimeout(async () => {
        try {
          const position = element.selectionStart;
          await dotNetHelper.invokeMethodAsync(
            "OnCursorPositionChanged",
            position,
          );
        } catch (error) {
          console.error("Error handling cursor movement:", error);
        }
      }, 10);
    }
  };

  // Input handler: updates cursor position when text changes (typing, deleting, pasting)
  const inputHandler = async (e) => {
    setTimeout(async () => {
      try {
        const position = element.selectionStart;
        await dotNetHelper.invokeMethodAsync(
          "OnCursorPositionChanged",
          position,
        );
      } catch (error) {
        console.error("Error handling input:", error);
      }
    }, 10);
  };

  // Store handler references for cleanup
  const handlers = {
    keydown: keydownHandler,
    click: clickHandler,
    keyup: keyupHandler,
    input: inputHandler,
  };
  autocompleteHandlers.set(element, handlers);

  // Attach event listeners (keydown uses capture phase to intercept early)
  element.addEventListener("keydown", keydownHandler, true);
  element.addEventListener("click", clickHandler);
  element.addEventListener("keyup", keyupHandler);
  element.addEventListener("input", inputHandler);
}

/**
 * Initialize keyboard shortcuts for PromptFields component
 * Handles Ctrl+Enter to trigger generate button
 * @param {HTMLElement} containerRef - The container element to attach keyboard handler
 * @param {Object} dotNetHelper - .NET object reference for interop callbacks
 */
export function initializePromptFieldsKeyboard(containerRef, dotNetHelper) {
  if (!containerRef) {
    console.error(
      "No container reference provided to initializePromptFieldsKeyboard",
    );
    return;
  }

  // Remove existing handler if present to avoid duplicates
  if (promptFieldsHandlers.has(containerRef)) {
    const oldHandler = promptFieldsHandlers.get(containerRef);
    containerRef.removeEventListener("keydown", oldHandler, true);
  }

  // Keydown handler: intercepts Ctrl+Enter and prompt-tab switching shortcuts.
  const keydownHandler = (e) => {
    // Check for Ctrl+Enter (or Cmd+Enter on Mac)
    if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
      // Prevent default behavior immediately
      e.preventDefault();
      e.stopPropagation();

      // Call .NET method asynchronously
      (async () => {
        try {
          await dotNetHelper.invokeMethodAsync("HandleCtrlEnter");
        } catch (error) {
          console.error("Error handling Ctrl+Enter:", error);
        }
      })();

      return false;
    }

    // Prompt tab switching: Ctrl+Tab (next), Ctrl+Shift+Tab (previous),
    // Alt+1 / Alt+2 (direct to tab index). Only intercepts within this
    // container so page-level Tab navigation is unaffected elsewhere.
    if ((e.ctrlKey || e.metaKey) && e.key === "Tab") {
      e.preventDefault();
      e.stopPropagation();
      const direction = e.shiftKey ? -1 : 1;
      (async () => {
        try {
          await dotNetHelper.invokeMethodAsync("HandleTabSwitch", direction);
        } catch (error) {
          console.error("Error handling prompt tab switch:", error);
        }
      })();
      return false;
    }

    if (
      e.altKey &&
      !e.ctrlKey &&
      !e.metaKey &&
      (e.key === "1" || e.key === "2" || e.key === "3")
    ) {
      e.preventDefault();
      e.stopPropagation();
      const index = parseInt(e.key, 10) - 1;
      (async () => {
        try {
          await dotNetHelper.invokeMethodAsync("HandleTabSelect", index);
        } catch (error) {
          console.error("Error handling prompt tab select:", error);
        }
      })();
      return false;
    }
  };

  // Store handler reference for cleanup
  promptFieldsHandlers.set(containerRef, keydownHandler);

  // Attach event listener (use capture phase to intercept early)
  containerRef.addEventListener("keydown", keydownHandler, true);

  // Make the container focusable if it isn't already
  if (!containerRef.hasAttribute("tabindex")) {
    containerRef.setAttribute("tabindex", "-1");
  }

  // Remove focus outline for better UX
  containerRef.style.outline = "none";
}

/**
 * Attach Enter-to-send handler to LLM instruction field
 * Prevents default newline insertion on Enter (without Shift/Ctrl/Alt)
 * @param {HTMLElement} fieldRef - The field container element
 */
export function attachLLMInstructionEnterHandler(fieldRef) {
  if (!fieldRef) return;

  const textarea = fieldRef.querySelector("textarea");
  if (!textarea || textarea.dataset.llmInstructionAttached === "1") return;

  textarea.dataset.llmInstructionAttached = "1";

  const keydownHandler = (e) => {
    if (e.key === "Enter" && !e.shiftKey && !e.ctrlKey && !e.altKey) {
      e.preventDefault();
    }
  };

  llmInstructionHandlers.set(textarea, keydownHandler);
  textarea.addEventListener("keydown", keydownHandler);
}

// Tracks wrapper -> resize handler mapping for autogrow, so we can re-invoke
// measurements on external value changes without rebinding listeners.
const autoGrowHandlers = new WeakMap();

/**
 * Turns the textarea inside `wrapperRef` into an auto-growing field.
 * Replaces MudBlazor's built-in AutoGrow because it misbehaves inside
 * deferred-mount containers (MudTabs) and with external value changes.
 *
 * @param {HTMLElement} wrapperRef - the MudTextField wrapper element
 * @param {number} maxLines - max lines before scrolling. 0 = uncapped.
 */
export function initializeAutoGrow(wrapperRef, maxLines) {
  if (!wrapperRef) return;
  const textarea = wrapperRef.querySelector("textarea");
  if (!textarea) {
    // Textarea may not be in DOM yet if MudTextField renders lazily.
    // Retry once on the next frame before giving up.
    requestAnimationFrame(() => {
      const retry = wrapperRef.querySelector("textarea");
      if (retry) bindAutoGrow(wrapperRef, retry, maxLines);
    });
    return;
  }
  bindAutoGrow(wrapperRef, textarea, maxLines);
}

function bindAutoGrow(wrapperRef, textarea, maxLines) {
  // Use !important to beat any MudBlazor scoped CSS that would clamp height.
  textarea.style.setProperty("resize", "none", "important");
  textarea.style.setProperty("overflow-y", "hidden", "important");
  textarea.style.setProperty("box-sizing", "border-box", "important");
  // Drop the rows attribute so the browser doesn't enforce a minimum height
  // that our inline height would have to fight.
  textarea.removeAttribute("rows");

  const resize = () => {
    const prevScroll = window.scrollY;
    textarea.style.setProperty("height", "auto", "important");
    let target = textarea.scrollHeight;
    if (maxLines && maxLines > 0) {
      const cs = getComputedStyle(textarea);
      const lh = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.4;
      const padTop = parseFloat(cs.paddingTop) || 0;
      const padBot = parseFloat(cs.paddingBottom) || 0;
      const cap = lh * maxLines + padTop + padBot;
      if (target > cap) {
        target = cap;
        textarea.style.setProperty("overflow-y", "auto", "important");
      } else {
        textarea.style.setProperty("overflow-y", "hidden", "important");
      }
    }
    textarea.style.setProperty("height", target + "px", "important");
    window.scrollTo({ top: prevScroll });
  };

  // Clean up any prior binding on this wrapper.
  const prior = autoGrowHandlers.get(wrapperRef);
  if (prior && prior.textarea) {
    prior.textarea.removeEventListener("input", prior.resize);
  }
  textarea.addEventListener("input", resize);
  autoGrowHandlers.set(wrapperRef, { textarea, resize });

  // Multi-pass first measurement: two rAFs cover MudTabs reveal + font loading,
  // and a final setTimeout covers any late style application.
  requestAnimationFrame(() =>
    requestAnimationFrame(() => {
      resize();
      setTimeout(resize, 120);
    }),
  );
}

/**
 * Forces a re-measure of a previously registered autogrow textarea.
 * Called on external Value changes (programmatic updates) so the field
 * reflows when content is set from outside user typing.
 */
export function resizeAutoGrow(wrapperRef) {
  if (!wrapperRef) return;
  const entry = autoGrowHandlers.get(wrapperRef);
  if (!entry) {
    // Not initialized yet (race between first render and parent re-render).
    // Try to initialize now if possible.
    const textarea = wrapperRef.querySelector("textarea");
    if (textarea) bindAutoGrow(wrapperRef, textarea, 0);
    return;
  }
  entry.resize();
}
