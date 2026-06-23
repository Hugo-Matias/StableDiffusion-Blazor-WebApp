/**
 * ImageEditor.layers.js - Layer management functionality
 */

import { LAYER_BASE, LAYER_MASK, generateId } from './ImageEditor.utils.js';

/**
 * Layer management mixin for ImageEditor
 */
export const LayerMixin = {
    
    /**
     * Initialize default layers
     */
    _initializeDefaultLayers() {
        this.layers = [
            {
                id: LAYER_BASE,
                name: 'Base Image',
                visible: true,
                locked: true,
                opacity: 1.0,
                order: 0,
                type: 'base'
            },
            {
                id: generateId(),
                name: 'Drawing Layer',
                visible: true,
                locked: false,
                opacity: 1.0,
                order: 1,
                type: 'drawing'
            }
        ];
        
        // Set the drawing layer as active
        this.activeLayerId = this.layers[1].id;
    },
    
    /**
     * Get the currently active layer
     */
    getActiveLayer() {
        return this.layers.find(l => l.id === this.activeLayerId);
    },
    
    /**
     * Set the active layer by ID
     */
    setActiveLayer(layerId) {
        const layer = this.layers.find(l => l.id === layerId);
        if (layer && !layer.locked) {
            this.activeLayerId = layerId;
            this._notifyLayerChanged();
            return true;
        }
        return false;
    },
    
    /**
     * Add a new layer
     */
    addLayer(name, type = 'drawing') {
        if (this.layers.length >= 20) {
            console.warn('Maximum layer count reached');
            return null;
        }
        
        const maxOrder = Math.max(...this.layers.filter(l => l.type !== 'mask').map(l => l.order));
        
        const layer = {
            id: generateId(),
            name: name || `Layer ${this.layers.length}`,
            visible: true,
            locked: false,
            opacity: 1.0,
            order: maxOrder + 1,
            type: type
        };
        
        this.layers.push(layer);
        this.activeLayerId = layer.id;
        this._notifyLayerChanged();
        
        return layer;
    },
    
    /**
     * Remove a layer by ID
     */
    removeLayer(layerId) {
        const layer = this.layers.find(l => l.id === layerId);
        if (!layer || layer.type === 'base' || layer.type === 'mask') {
            return false;
        }
        
        // Remove all objects belonging to this layer
        const objectsToRemove = this.canvas.getObjects().filter(obj => obj.layerId === layerId);
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        
        // Remove layer from list
        this.layers = this.layers.filter(l => l.id !== layerId);
        
        // If removed layer was active, select another
        if (this.activeLayerId === layerId) {
            const availableLayers = this.layers.filter(l => l.type !== 'base' && l.type !== 'mask');
            this.activeLayerId = availableLayers.length > 0 
                ? availableLayers.sort((a, b) => b.order - a.order)[0].id 
                : null;
        }
        
        this.canvas.renderAll();
        this._notifyLayerChanged();
        
        return true;
    },
    
    /**
     * Update layer properties
     */
    updateLayer(layerId, props) {
        const layer = this.layers.find(l => l.id === layerId);
        if (!layer) return false;
        
        if (props.name !== undefined) layer.name = props.name;
        if (props.visible !== undefined) {
            layer.visible = props.visible;
            this._updateLayerVisibility(layerId, props.visible);
        }
        if (props.locked !== undefined) {
            layer.locked = props.locked;
            this._updateLayerLocked(layerId, props.locked);
        }
        if (props.opacity !== undefined) {
            layer.opacity = props.opacity;
            this._updateLayerOpacity(layerId, props.opacity);
        }
        if (props.order !== undefined) layer.order = props.order;
        
        this._notifyLayerChanged();
        return true;
    },
    
    /**
     * Update visibility of all objects in a layer
     */
    _updateLayerVisibility(layerId, visible) {
        const objects = this.canvas.getObjects().filter(obj => obj.layerId === layerId);
        objects.forEach(obj => obj.set('visible', visible));
        
        // Special handling for base image
        if (layerId === LAYER_BASE && this.baseImageObject) {
            this.baseImageObject.set('visible', visible);
        }
        
        this.canvas.renderAll();
    },
    
    /**
     * Update locked state of all objects in a layer
     */
    _updateLayerLocked(layerId, locked) {
        const objects = this.canvas.getObjects().filter(obj => obj.layerId === layerId);
        objects.forEach(obj => {
            obj.set({
                selectable: !locked && this.currentTool === 'select',
                evented: !locked && this.currentTool === 'select',
                lockMovementX: locked,
                lockMovementY: locked,
                lockRotation: locked,
                lockScalingX: locked,
                lockScalingY: locked,
                hasControls: !locked,
                hasBorders: !locked
            });
        });
        
        // If the locked layer is active, deselect any selected objects
        if (locked && this.activeLayerId === layerId) {
            this.canvas.discardActiveObject();
        }
        
        this.canvas.renderAll();
    },
    
    /**
     * Update opacity of all objects in a layer
     */
    _updateLayerOpacity(layerId, opacity) {
        const objects = this.canvas.getObjects().filter(obj => obj.layerId === layerId);
        objects.forEach(obj => obj.set('opacity', opacity));
        
        // Special handling for base image
        if (layerId === LAYER_BASE && this.baseImageObject) {
            this.baseImageObject.set('opacity', opacity);
        }
        
        this.canvas.renderAll();
    },
    
    /**
     * Bring an object to the front of the canvas
     * Handles both Fabric.js 5.x and 6.x API
     */
    _bringObjectToFront(obj) {
        if (!obj || !this.canvas) return;
        
        // Try Fabric.js 6.x API first (method on object)
        if (typeof obj.bringToFront === 'function') {
            obj.bringToFront();
        }
        // Try Fabric.js 5.x API (method on canvas)
        else if (typeof this.canvas.bringToFront === 'function') {
            this.canvas.bringToFront(obj);
        }
        // Fallback: move to end of objects array
        else {
            const objects = this.canvas.getObjects();
            const index = objects.indexOf(obj);
            if (index > -1 && index < objects.length - 1) {
                this.canvas.remove(obj);
                this.canvas.add(obj);
            }
        }
    },
    
    /**
     * Reorder a single layer in a direction
     * @param {string} layerId - The layer ID to move
     * @param {number} direction - 1 for up (move towards top of stack/higher order), -1 for down (lower order)
     */
    reorderLayer(layerId, direction) {
        const layer = this.layers.find(l => l.id === layerId);
        if (!layer || layer.type === 'base') return false;
        
        // Get reorderable layers sorted by order (ascending - lowest order first)
        const reorderableLayers = this.layers
            .filter(l => l.type !== 'base' && l.type !== 'mask')
            .sort((a, b) => a.order - b.order);
        
        const currentIndex = reorderableLayers.findIndex(l => l.id === layerId);
        if (currentIndex < 0) return false;
        
        // direction 1 = move up (increase order, swap with layer at higher index in sorted array)
        // direction -1 = move down (decrease order, swap with layer at lower index in sorted array)
        const targetIndex = currentIndex + direction;
        
        if (targetIndex < 0 || targetIndex >= reorderableLayers.length) return false;
        
        // Swap orders between current layer and target layer
        const targetLayer = reorderableLayers[targetIndex];
        const tempOrder = layer.order;
        layer.order = targetLayer.order;
        targetLayer.order = tempOrder;
        
        // Reorder objects on canvas
        this._reorderCanvasObjects();
        this._notifyLayerChanged();
        
        return true;
    },
    
    /**
     * Reorder layers - accepts an array of layer IDs in desired order
     */
    reorderLayers(layerIds) {
        if (!Array.isArray(layerIds)) return;
        
        layerIds.forEach((id, index) => {
            const layer = this.layers.find(l => l.id === id);
            if (layer) {
                layer.order = index;
            }
        });
        
        this._reorderCanvasObjects();
        this._notifyLayerChanged();
    },
    
    /**
     * Reorder canvas objects based on layer order
     */
    _reorderCanvasObjects() {
        // Sort layers by order
        const sortedLayers = [...this.layers].sort((a, b) => a.order - b.order);
        
        // Move objects to match layer order
        sortedLayers.forEach(layer => {
            const objects = this.canvas.getObjects().filter(obj => obj.layerId === layer.id);
            objects.forEach(obj => {
                this._bringObjectToFront(obj);
            });
        });
        
        // Ensure mask objects are always on top
        this.maskObjects.forEach(obj => {
            this._bringObjectToFront(obj);
        });
        
        // Brush cursor always on very top
        if (this._brushCursorOuter) this._bringObjectToFront(this._brushCursorOuter);
        if (this._brushCursorInner) this._bringObjectToFront(this._brushCursorInner);
        
        this.canvas.renderAll();
    },
    
    /**
     * Get all layers for UI
     */
    getLayers() {
        return this.layers.map(l => ({...l}));
    },
    
    /**
     * Get layer state for serialization
     */
    getLayerState() {
        return {
            layers: this.layers.map(l => ({...l})),
            activeLayerId: this.activeLayerId
        };
    },
    
    /**
     * Restore layer state from serialization
     */
    restoreLayerState(state) {
        if (state && state.layers && Array.isArray(state.layers)) {
            this.layers = state.layers.map(l => ({...l}));
            this.activeLayerId = state.activeLayerId;
            
            // Ensure the active layer exists in the restored layers
            if (!this.layers.find(l => l.id === this.activeLayerId)) {
                // Default to first non-base, non-mask layer
                const defaultLayer = this.layers.find(l => l.type !== 'base' && l.type !== 'mask');
                this.activeLayerId = defaultLayer ? defaultLayer.id : null;
            }
        }
    },
    
    /**
     * Copy selected objects to clipboard (callable from C#)
     */
    copySelection() {
        if (typeof this._copySelectedObjects === 'function') {
            this._copySelectedObjects();
        }
    },
    
    /**
     * Paste objects from clipboard (callable from C#)
     */
    pasteClipboard() {
        if (typeof this._pasteObjects === 'function') {
            this._pasteObjects();
        }
    },
    
    /**
     * Duplicate selected objects (callable from C#)
     */
    duplicateSelection() {
        if (typeof this._duplicateSelectedObjects === 'function') {
            this._duplicateSelectedObjects();
        }
    },
    
    /**
     * Flip selected objects (callable from C#)
     * @param {string} direction - 'horizontal' or 'vertical'
     */
    flipSelection(direction) {
        if (typeof this._flipSelectedObjects === 'function') {
            this._flipSelectedObjects(direction);
        }
    },
    
    /**
     * Delete selected objects (callable from C#)
     */
    deleteSelection() {
        if (typeof this._deleteSelectedObjects === 'function') {
            this._deleteSelectedObjects();
        }
    }
};
