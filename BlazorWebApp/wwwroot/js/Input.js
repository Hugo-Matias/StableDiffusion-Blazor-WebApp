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
        console.error('Error getting caret position:', e);
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
            range.setStart(element.firstChild, Math.min(position, element.textContent.length));
            range.collapse(true);
            sel.removeAllRanges();
            sel.addRange(range);
            element.focus();
        }
    } catch (e) {
        console.error('Error setting caret position:', e);
    }
};

// Store event handlers to avoid memory leaks
const autocompleteHandlers = new WeakMap();

/**
 * Initialize autocomplete functionality on a text input/textarea element
 * @param {HTMLElement} wrapperRef - The wrapper element containing the input
 * @param {Object} dotNetHelper - .NET object reference for interop callbacks
 */
export function initializeAutocomplete(wrapperRef, dotNetHelper) {
    if (!wrapperRef) {
        console.error('No element reference provided to initializeAutocomplete');
        return;
    }

    // Find the actual input/textarea element within the wrapper
    let element = wrapperRef.querySelector('input') || wrapperRef.querySelector('textarea');

    if (!element) {
        console.error('Could not find input or textarea element');
        return;
    }

    // Remove existing handlers if present to avoid duplicates
    if (autocompleteHandlers.has(element)) {
        const oldHandlers = autocompleteHandlers.get(element);
        element.removeEventListener('keydown', oldHandlers.keydown, true);
        element.removeEventListener('click', oldHandlers.click);
        element.removeEventListener('keyup', oldHandlers.keyup);
    }

    // Keydown handler: intercepts navigation keys when dropdown is visible
    const keydownHandler = (e) => {
        const shouldHandle = e.key === 'ArrowDown' || e.key === 'ArrowUp' || 
                           e.key === 'Enter' || e.key === 'Tab' || e.key === 'Escape';
        
        if (!shouldHandle) return;

        // Check dropdown visibility synchronously
        const dropdown = wrapperRef.querySelector('.autocomplete-dropdown');
        const isVisible = dropdown && dropdown.offsetParent !== null;
        
        if (isVisible) {
            // Prevent default behavior immediately before async operations
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            
            // Handle the key asynchronously
            (async () => {
                try {
                    await dotNetHelper.invokeMethodAsync('HandleKeyDownFromJS', e.key);
                } catch (error) {
                    console.error('Error handling keydown in autocomplete:', error);
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
                await dotNetHelper.invokeMethodAsync('OnCursorPositionChanged', position);
            } catch (error) {
                console.error('Error handling click:', error);
            }
        }, 10);
    };

    // Keyup handler: updates cursor position on arrow key navigation
    const keyupHandler = async (e) => {
        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight' || e.key === 'Home' || e.key === 'End') {
            setTimeout(async () => {
                try {
                    const position = element.selectionStart;
                    await dotNetHelper.invokeMethodAsync('OnCursorPositionChanged', position);
                } catch (error) {
                    console.error('Error handling cursor movement:', error);
                }
            }, 10);
        }
    };

    // Store handler references for cleanup
    const handlers = {
        keydown: keydownHandler,
        click: clickHandler,
        keyup: keyupHandler
    };
    autocompleteHandlers.set(element, handlers);

    // Attach event listeners (keydown uses capture phase to intercept early)
    element.addEventListener('keydown', keydownHandler, true);
    element.addEventListener('click', clickHandler);
    element.addEventListener('keyup', keyupHandler);
}