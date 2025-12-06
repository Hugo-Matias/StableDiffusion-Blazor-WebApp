// ImageInput.js - Handles drag/drop, paste, and file input interactions

export function init(dropzoneElement, inputElement, dotNetRef) {
    const instance = new ImageInputHandler(dropzoneElement, inputElement, dotNetRef);
    return instance;
}

class ImageInputHandler {
    constructor(dropzoneElement, inputElement, dotNetRef) {
        this.dropzone = dropzoneElement;
        this.input = inputElement;
        this.dotNetRef = dotNetRef;
        
        this.setupEventListeners();
    }
    
    setupEventListeners() {
        // Drag and drop events
        this.dropzone.addEventListener('dragenter', this.handleDragEnter.bind(this));
        this.dropzone.addEventListener('dragleave', this.handleDragLeave.bind(this));
        this.dropzone.addEventListener('dragover', this.handleDragOver.bind(this));
        this.dropzone.addEventListener('drop', this.handleDrop.bind(this));
        
        // Paste event (global, but we'll filter)
        document.addEventListener('paste', this.handlePaste.bind(this));
        
        // Click to open file dialog
        this.dropzone.addEventListener('click', (e) => {
            // Don't trigger if clicking on buttons
            if (e.target.closest('button')) return;
            if (this.input) {
                this.input.click();
            }
        });
    }
    
    handleDragEnter(e) {
        e.preventDefault();
        e.stopPropagation();
        this.dotNetRef.invokeMethodAsync('OnDragEnter');
    }
    
    handleDragLeave(e) {
        e.preventDefault();
        e.stopPropagation();
        // Only trigger if we're leaving the dropzone entirely
        if (!this.dropzone.contains(e.relatedTarget)) {
            this.dotNetRef.invokeMethodAsync('OnDragLeave');
        }
    }
    
    handleDragOver(e) {
        e.preventDefault();
        e.stopPropagation();
    }
    
    handleDrop(e) {
        e.preventDefault();
        e.stopPropagation();
        this.dotNetRef.invokeMethodAsync('OnDrop');
        
        const files = e.dataTransfer.files;
        if (files.length > 0 && files[0].type.startsWith('image/')) {
            this.loadImageFile(files[0]);
        }
    }
    
    handlePaste(e) {
        // Only handle paste if this dropzone or its children are focused
        // Or if the paste contains image data
        const items = e.clipboardData?.items;
        if (!items) return;
        
        for (const item of items) {
            if (item.type.startsWith('image/')) {
                e.preventDefault();
                const file = item.getAsFile();
                if (file) {
                    this.loadImageFile(file);
                }
                break;
            }
        }
    }
    
    async pasteFromClipboard() {
        try {
            const clipboardItems = await navigator.clipboard.read();
            for (const item of clipboardItems) {
                for (const type of item.types) {
                    if (type.startsWith('image/')) {
                        const blob = await item.getType(type);
                        this.loadImageFile(blob);
                        return;
                    }
                }
            }
        } catch (err) {
            console.warn('Failed to read clipboard:', err);
            // Fallback: try to trigger paste event
        }
    }
    
    loadImageFile(file) {
        const reader = new FileReader();
        reader.onload = (e) => {
            const imageData = e.target.result;
            this.dotNetRef.invokeMethodAsync('OnImagePasted', imageData);
        };
        reader.readAsDataURL(file);
    }
    
    dispose() {
        document.removeEventListener('paste', this.handlePaste.bind(this));
    }
}
