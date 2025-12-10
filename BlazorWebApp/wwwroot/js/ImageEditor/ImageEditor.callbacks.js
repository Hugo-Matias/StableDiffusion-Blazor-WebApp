/**
 * ImageEditor.callbacks.js - .NET interop callbacks
 */

/**
 * Callbacks mixin for ImageEditor
 */
export const CallbacksMixin = {
    
    /**
     * Notify .NET that an image was loaded
     */
    _notifyImageLoaded(width, height) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnImageLoaded', width, height);
        }
    },
    
    /**
     * Notify .NET of zoom change (debounced)
     */
    _notifyZoomChangedDebounced(zoom) {
        if (this._isSettingZoomFromExternal) {
            return;
        }
        
        if (this._zoomNotifyTimeout) {
            clearTimeout(this._zoomNotifyTimeout);
        }
        
        this._zoomNotifyTimeout = setTimeout(() => {
            this._notifyZoomChanged(zoom);
        }, 100);
    },
    
    /**
     * Notify .NET of zoom change
     */
    _notifyZoomChanged(zoom) {
        if (this.dotNetRef && !this._isSettingZoomFromExternal) {
            this.dotNetRef.invokeMethodAsync('OnZoomChanged', zoom);
        }
    },
    
    /**
     * Notify .NET of color picked
     */
    _notifyColorPicked(color) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnColorPicked', color);
        }
    },
    
    /**
     * Notify .NET of tool change request
     */
    _notifyToolChange(tool) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnToolChangeRequested', tool);
        }
    },
    
    /**
     * Notify .NET of brush size change request
     */
    _notifyBrushSizeChange(delta) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnBrushSizeChangeRequested', delta);
        }
    },
    
    /**
     * Notify .NET of cursor position
     */
    _notifyCursorPosition(x, y) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCursorPositionChanged', x, y);
        }
    },
    
    /**
     * Notify .NET to save state
     */
    _notifySaveState(state) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnSaveState', state);
        }
    },
    
    /**
     * Notify .NET of undo request
     */
    _notifyRequestUndo() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnUndoRequested');
        }
    },
    
    /**
     * Notify .NET of redo request
     */
    _notifyRequestRedo() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnRedoRequested');
        }
    },
    
    /**
     * Notify .NET of mask change
     */
    _notifyMaskChanged(hasMask) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnMaskChanged', hasMask);
        }
    },
    
    /**
     * Notify .NET of layer change
     */
    _notifyLayerChanged() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnLayerChanged', this.getLayerState());
        }
    },
    
    /**
     * Notify .NET of image import
     */
    _notifyImageImported(width, height) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnImageImported', Math.round(width), Math.round(height));
        }
    },
    
    /**
     * Notify .NET of selection change
     */
    _notifySelectionChanged(hasSelection) {
        if (this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync('OnSelectionChanged', hasSelection);
            } catch (e) {
                console.warn('Failed to notify selection changed:', e);
            }
        }
    },
    
    /**
     * Notify .NET of canvas modification
     */
    _notifyCanvasModified() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCanvasModified');
        }
    }
};
