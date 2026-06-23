// Keyboard shortcuts for PromptsPanel
let dotNetRef = null;
let containerElement = null;
let isDisposed = false;

export function initializeKeyboardShortcuts(container, dotNetReference) {
    dotNetRef = dotNetReference;
    containerElement = container;
    isDisposed = false;
    
    // Attach keyboard event listener to the container
    if (containerElement) {
        containerElement.addEventListener('keydown', handleKeyDown);
        
        // Also attach to window for global shortcuts
        window.addEventListener('keydown', handleKeyDown);
    }
}

function handleKeyDown(event) {
    if (isDisposed || !dotNetRef) return;
    
    // Check if Ctrl key is pressed and a number key (1-9)
    if (event.ctrlKey && !event.altKey && !event.metaKey) {
        const key = event.key;
        const keyCode = parseInt(key);
        
        // Check if it's a number between 1-9
        if (keyCode >= 1 && keyCode <= 9) {
            event.preventDefault(); // Prevent browser default behavior
            
            const index = keyCode - 1; // Convert to 0-based index
            
            if (event.shiftKey) {
                // Ctrl+Shift+1-9: Apply to Img2Img
                dotNetRef.invokeMethodAsync('ApplyPinnedPromptToImg2ImgByIndex', index)
                    .catch(err => {
                        // Silently handle errors
                        console.debug('Failed to apply pinned prompt to Img2Img:', err);
                    });
            } else {
                // Ctrl+1-9: Apply to Txt2Img
                dotNetRef.invokeMethodAsync('ApplyPinnedPromptByIndex', index)
                    .catch(err => {
                        // Silently handle errors
                        console.debug('Failed to apply pinned prompt to Txt2Img:', err);
                    });
            }
        }
    }
}

export function dispose() {
    isDisposed = true;
    
    if (containerElement) {
        containerElement.removeEventListener('keydown', handleKeyDown);
    }
    
    window.removeEventListener('keydown', handleKeyDown);
    
    dotNetRef = null;
    containerElement = null;
}
