/**
 * ImageEditor.history.js - History management (undo/redo) and state serialization
 */

import { LAYER_MASK } from './ImageEditor.utils.js';

/**
 * History management mixin for ImageEditor
 */
export const HistoryMixin = {
    
    /**
     * Trigger undo
     */
    undo() {
        this._notifyRequestUndo();
    },
    
    /**
     * Trigger redo
     */
    redo() {
        this._notifyRequestRedo();
    },
    
    /**
     * Save current state
     */
    saveState() {
        this._saveHistoryState();
    },
    
    /**
     * Save state before an action begins (for proper undo)
     */
    _saveStateBeforeAction() {
        if (this.historyLocked || this._stateBeforeActionSaved || !this.canvas.isDrawingMode) {
            return;
        }
        
        this._pendingUndoState = this.getState();
        this._stateBeforeActionSaved = true;
    },
    
    /**
     * Called after path is created to save the captured state
     */
    _afterPathCreated() {
        if (this._pendingUndoState) {
            this._notifySaveState(this._pendingUndoState);
            this._pendingUndoState = null;
        }
        this._stateBeforeActionSaved = false;
    },
    
    /**
     * Save history state
     */
    _saveHistoryState() {
        if (this.historyLocked) return;
        
        const state = this.getState();
        this._notifySaveState(state);
    },
    
    /**
     * Load a saved state
     */
    loadState(json) {
        if (!json) return;
        
        this.historyLocked = true;
        
        const currentVPT = [...this.canvas.viewportTransform];
        
        let stateData;
        try {
            stateData = JSON.parse(json);
        } catch (e) {
            console.error('Failed to parse state JSON:', e);
            this.historyLocked = false;
            return;
        }
        
        // Restore layer state if present
        if (stateData.layerState) {
            this.restoreLayerState(stateData.layerState);
        }
        
        const objectsToRestore = stateData.objects?.filter(
            objData => objData.name === 'drawing' || objData.name === 'mask' || objData.name === 'imported'
        ) || [];
        
        // Remove all current drawings and masks
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing' || obj.name === 'mask' || obj.name === 'imported' || obj.name === '_brushCursor'
        );
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        
        this.maskObjects = [];
        
        if (objectsToRestore.length === 0) {
            this.canvas.setViewportTransform(currentVPT);
            this.canvas.renderAll();
            this.historyLocked = false;
            this._notifyMaskChanged(false);
            this._notifyLayerChanged();
            return;
        }
        
        this._restoreObjectsDirectly(objectsToRestore, currentVPT);
    },
    
    /**
     * Restore objects directly from data
     */
    _restoreObjectsDirectly(objectsData, viewportTransform) {
        const imagePromises = [];
        
        objectsData.forEach(objData => {
            try {
                const objType = (objData.type || '').toLowerCase();
                
                if (objType === 'path' && objData.path) {
                    const path = new fabric.Path(objData.path, {
                        left: objData.left || 0,
                        top: objData.top || 0,
                        fill: objData.fill,
                        stroke: objData.stroke,
                        strokeWidth: objData.strokeWidth,
                        strokeLineCap: objData.strokeLineCap,
                        strokeLineJoin: objData.strokeLineJoin,
                        opacity: objData.opacity !== undefined ? objData.opacity : 1,
                        selectable: false,
                        evented: false,
                        hasControls: false,
                        hasBorders: false
                    });
                    
                    path.name = objData.name;
                    path.layerId = objData.layerId || this.activeLayerId;
                    path.layer = objData.layer;
                    
                    const originalToObject = path.toObject.bind(path);
                    path.toObject = function(propertiesToInclude) {
                        const result = originalToObject(propertiesToInclude);
                        result.name = this.name;
                        result.layerId = this.layerId;
                        result.layer = this.layer;
                        return result;
                    };
                    
                    this.canvas.add(path);
                    
                    if (objData.name === 'mask') {
                        this.maskObjects.push(path);
                    }
                }
                else if (objType === 'image' && objData.src) {
                    const imgPromise = this._restoreImageObject(objData);
                    imagePromises.push(imgPromise);
                }
            } catch (err) {
                console.error('Failed to restore object:', err);
            }
        });
        
        if (imagePromises.length > 0) {
            Promise.all(imagePromises).then(() => {
                this._finalizeStateRestore(viewportTransform);
            });
        } else {
            this._finalizeStateRestore(viewportTransform);
        }
    },
    
    /**
     * Restore a single image object
     */
    async _restoreImageObject(objData) {
        try {
            let img;
            const result = fabric.Image.fromURL(objData.src, { crossOrigin: 'anonymous' });
            
            if (result instanceof Promise) {
                img = await result;
            } else {
                img = await new Promise((res, rej) => {
                    fabric.Image.fromURL(objData.src, (loadedImg) => {
                        if (loadedImg) res(loadedImg);
                        else rej(new Error('Failed to load image'));
                    }, { crossOrigin: 'anonymous' });
                });
            }
            
            if (img) {
                img.set({
                    left: objData.left || 0,
                    top: objData.top || 0,
                    scaleX: objData.scaleX || 1,
                    scaleY: objData.scaleY || 1,
                    angle: objData.angle || 0,
                    flipX: objData.flipX || false,
                    flipY: objData.flipY || false,
                    originX: objData.originX || 'left',
                    originY: objData.originY || 'top',
                    opacity: objData.opacity !== undefined ? objData.opacity : 1,
                    selectable: this.currentTool === 'select',
                    evented: this.currentTool === 'select',
                    hasControls: true,
                    hasBorders: true,
                    cornerStyle: 'circle',
                    cornerColor: '#4285f4',
                    cornerStrokeColor: '#ffffff',
                    borderColor: '#4285f4',
                    transparentCorners: false
                });
                
                img.name = objData.name || 'imported';
                img.layerId = objData.layerId || this.activeLayerId;
                img.layer = objData.layer || 'Drawing Layer';
                
                const originalToObject = img.toObject.bind(img);
                img.toObject = function(propertiesToInclude) {
                    const result = originalToObject(propertiesToInclude);
                    result.name = this.name;
                    result.layerId = this.layerId;
                    result.layer = this.layer;
                    return result;
                };
                
                this.canvas.add(img);
            }
        } catch (err) {
            console.error('Failed to restore image:', err);
        }
    },
    
    /**
     * Finalize state restoration
     */
    _finalizeStateRestore(viewportTransform) {
        // Reorder canvas objects based on layer order
        this._reorderCanvasObjects();
        
        // Apply layer visibility, opacity, and locked state to restored objects
        this.layers.forEach(layer => {
            this._updateLayerVisibility(layer.id, layer.visible);
            this._updateLayerOpacity(layer.id, layer.opacity);
            this._updateLayerLocked(layer.id, layer.locked);
        });
        
        this.canvas.setViewportTransform(viewportTransform);
        this.canvas.renderAll();
        this.historyLocked = false;
        this._notifyMaskChanged(this.maskObjects.length > 0);
        this._notifyLayerChanged();
    },
    
    /**
     * Get current canvas state as JSON (includes layer state)
     */
    getState() {
        const canvasState = this.canvas.toJSON(['name', 'layerId', 'layer']);
        
        // Include layer state in the serialization
        canvasState.layerState = this.getLayerState();
        
        return JSON.stringify(canvasState);
    }
};
