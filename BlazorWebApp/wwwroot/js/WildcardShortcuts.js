// Keyboard shortcuts for WildcardsTab
let dotNetRef = null;
let containerElement = null;
let isDisposed = false;

export function initializeWildcardShortcuts(container, dotNetReference) {
    dotNetRef = dotNetReference;
    containerElement = container;
    isDisposed = false;
    
    if (containerElement) {
        containerElement.addEventListener('keydown', handleKeyDown);
    }
}

function handleKeyDown(event) {
    if (isDisposed || !dotNetRef) return;
    
    // Ignore if typing in an input field
    const tagName = event.target.tagName.toLowerCase();
    if (tagName === 'input' || tagName === 'textarea') {
        return;
    }
    
    // Ctrl+N: New Collection
    if (event.ctrlKey && !event.shiftKey && event.key.toLowerCase() === 'n') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('CreateNewCollection')
            .catch(err => console.debug('Failed to create collection:', err));
        return;
    }
    
    // Ctrl+I: Import Collection
    if (event.ctrlKey && !event.shiftKey && event.key.toLowerCase() === 'i') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('ImportCollection')
            .catch(err => console.debug('Failed to import:', err));
        return;
    }
    
    // Ctrl+E: Edit Selected Collection/Entry
    if (event.ctrlKey && !event.shiftKey && event.key.toLowerCase() === 'e') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('EditSelected')
            .catch(err => console.debug('Failed to edit:', err));
        return;
    }
    
    // Ctrl+Shift+E: Export Selected Collection
    if (event.ctrlKey && event.shiftKey && event.key.toLowerCase() === 'e') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('ExportCollection')
            .catch(err => console.debug('Failed to export:', err));
        return;
    }
    
    // Delete: Delete Selected
    if (event.key === 'Delete') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('DeleteSelected')
            .catch(err => console.debug('Failed to delete:', err));
        return;
    }
    
    // Ctrl+F: Focus Search
    if (event.ctrlKey && !event.shiftKey && event.key.toLowerCase() === 'f') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('FocusSearch')
            .catch(err => console.debug('Failed to focus search:', err));
        return;
    }
    
    // Arrow Up: Move Entry Up
    if (event.ctrlKey && event.key === 'ArrowUp') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('MoveEntryUp')
            .catch(err => console.debug('Failed to move up:', err));
        return;
    }
    
    // Arrow Down: Move Entry Down
    if (event.ctrlKey && event.key === 'ArrowDown') {
        event.preventDefault();
        dotNetRef.invokeMethodAsync('MoveEntryDown')
            .catch(err => console.debug('Failed to move down:', err));
        return;
    }
}

export function dispose() {
    isDisposed = true;
    
    if (containerElement) {
        containerElement.removeEventListener('keydown', handleKeyDown);
    }
    
    dotNetRef = null;
    containerElement = null;
}
