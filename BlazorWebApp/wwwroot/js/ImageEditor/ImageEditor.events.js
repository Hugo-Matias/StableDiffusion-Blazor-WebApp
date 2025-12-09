/**
 * ImageEditor.events.js - Event handlers for mouse, keyboard, and canvas events
 */

import { LAYER_MASK, boundsIntersect } from './ImageEditor.utils.js';

/**
 * Events mixin for ImageEditor
 */
export const EventsMixin = {
    
    /**
     * Setup all event listeners
     */
    _setupEventListeners() {
        // Fabric.js canvas events
        this.canvas.on('mouse:wheel', (opt) => this._handleMouseWheel(opt));
        this.canvas.on('mouse:down', (opt) => this._handleMouseDown(opt));
        this.canvas.on('mouse:move', (opt) => this._handleMouseMove(opt));
        this.canvas.on('mouse:up', (opt) => this._handleMouseUp(opt));
        this.canvas.on('mouse:out', () => this._hideBrushCursor());
        this.canvas.on('path:created', (opt) => this._handlePathCreated(opt));
        
        // Keyboard events
        document.addEventListener('keydown', this._boundKeyDown);
        document.addEventListener('keyup', this._boundKeyUp);
        window.addEventListener('resize', this._boundResize);
        
        // Setup middle mouse button handling using native DOM events
        this._setupMiddleMouseHandling();
    },
    
    /**
     * Setup middle mouse button handling to enable panning
     * Uses native DOM events to capture before Fabric.js
     */
    _setupMiddleMouseHandling() {
        const upperCanvas = this.canvas.upperCanvasEl;
        if (!upperCanvas) return;
        
        // Store bound handlers for cleanup
        this._boundNativeMouseDown = this._handleNativeMouseDown.bind(this);
        this._boundNativeMouseMove = this._handleNativeMouseMove.bind(this);
        this._boundNativeMouseUp = this._handleNativeMouseUp.bind(this);
        
        // Add native event listeners on the upper canvas (captures events before Fabric.js)
        upperCanvas.addEventListener('mousedown', this._boundNativeMouseDown, { capture: true });
        upperCanvas.addEventListener('auxclick', (e) => { e.preventDefault(); e.stopPropagation(); });
        
        // Add document-level handlers for move/up to allow panning outside canvas
        document.addEventListener('mousemove', this._boundNativeMouseMove);
        document.addEventListener('mouseup', this._boundNativeMouseUp);
        
        // Prevent context menu on the canvas
        upperCanvas.addEventListener('contextmenu', (e) => e.preventDefault());
    },
    
    /**
     * Native mouse down handler for middle mouse button
     */
    _handleNativeMouseDown(e) {
        if (e.button === 1) {
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            
            this._middleMousePanning = true;
            this._middleMouseLastPoint = { x: e.clientX, y: e.clientY };
            this._savedDrawingModeForMiddle = this.canvas.isDrawingMode;
            this.canvas.isDrawingMode = false;
            this.canvas.defaultCursor = 'grabbing';
            this._hideBrushCursor();
            
            // Change cursor on the canvas element
            this.canvas.upperCanvasEl.style.cursor = 'grabbing';
        }
    },
    
    /**
     * Native mouse move handler for middle mouse panning
     */
    _handleNativeMouseMove(e) {
        if (this._middleMousePanning && this._middleMouseLastPoint) {
            const deltaX = e.clientX - this._middleMouseLastPoint.x;
            const deltaY = e.clientY - this._middleMouseLastPoint.y;
            
            // Apply pan
            const vpt = this.canvas.viewportTransform;
            vpt[4] += deltaX;
            vpt[5] += deltaY;
            this.canvas.setViewportTransform(vpt);
            this.canvas.renderAll();
            
            this._middleMouseLastPoint = { x: e.clientX, y: e.clientY };
        }
    },
    
    /**
     * Native mouse up handler for middle mouse panning
     */
    _handleNativeMouseUp(e) {
        if (e.button === 1 && this._middleMousePanning) {
            e.preventDefault();
            e.stopPropagation();
            
            this._middleMousePanning = false;
            this._middleMouseLastPoint = null;
            
            // Restore drawing mode
            if (this._savedDrawingModeForMiddle !== undefined) {
                this.canvas.isDrawingMode = this._savedDrawingModeForMiddle;
                this._savedDrawingModeForMiddle = undefined;
            }
            
            // Restore cursor
            this.setTool(this.currentTool);
        }
    },
    
    /**
     * Prevent middle mouse scroll
     */
    _preventMiddleScroll(e) {
        if (e.button === 1) {
            e.preventDefault();
            e.stopPropagation();
            return false;
        }
    },
    
    /**
     * Handle mouse wheel (zoom)
     */
    _handleMouseWheel(opt) {
        const delta = opt.e.deltaY;
        let zoom = this.canvas.getZoom();
        
        const factor = 0.999 ** delta;
        zoom *= factor;
        zoom = Math.min(Math.max(zoom, 0.1), 5.0);
        
        this.canvas.zoomToPoint(
            new fabric.Point(opt.e.offsetX, opt.e.offsetY),
            zoom
        );
        
        this._updateBrushSize();
        this._notifyZoomChangedDebounced(zoom);
        
        opt.e.preventDefault();
        opt.e.stopPropagation();
    },
    
    /**
     * Handle mouse down (Fabric.js event)
     */
    _handleMouseDown(opt) {
        const e = opt.e;
        
        // Skip if middle mouse panning is active (handled by native events)
        if (this._middleMousePanning) {
            return;
        }
        
        // Middle mouse button - already handled by native events, but prevent default here too
        if (e.button === 1) {
            return;
        }
        
        // Space + left click for panning
        if (this.isSpacePressed && e.button === 0) {
            e.preventDefault();
            this.isPanning = true;
            this.lastPanPoint = { x: e.clientX, y: e.clientY };
            this._savedDrawingMode = this.canvas.isDrawingMode;
            this.canvas.isDrawingMode = false;
            this.canvas.defaultCursor = 'grabbing';
            this._hideBrushCursor();
            return;
        }
        
        // Pan tool
        if (this.currentTool === 'pan' && e.button === 0) {
            this.isPanning = true;
            this.lastPanPoint = { x: e.clientX, y: e.clientY };
            this.canvas.defaultCursor = 'grabbing';
            return;
        }
        
        // Color picker on click
        if (this.currentTool === 'colorpicker' && e.button === 0) {
            const color = this.getColorAtPoint(e.offsetX, e.offsetY);
            this._notifyColorPicked(color);
            return;
        }
        
        // Save state before drawing starts
        if (this.canvas.isDrawingMode && e.button === 0) {
            this._saveStateBeforeAction();
        }
    },
    
    /**
     * Handle mouse move (Fabric.js event)
     */
    _handleMouseMove(opt) {
        const e = opt.e;
        
        // Skip if middle mouse panning is active (handled by native events)
        if (this._middleMousePanning) {
            return;
        }
        
        // Handle space/tool panning
        if (this.isPanning && this.lastPanPoint) {
            const deltaX = e.clientX - this.lastPanPoint.x;
            const deltaY = e.clientY - this.lastPanPoint.y;
            this.pan(deltaX, deltaY);
            this.lastPanPoint = { x: e.clientX, y: e.clientY };
            return;
        }
        
        // Update brush cursor position
        if (this.canvas.isDrawingMode && !this.isPanning) {
            const pointer = this.canvas.getPointer(e);
            this._updateBrushCursor(pointer.x, pointer.y);
        }
        
        // Update cursor position for .NET (throttled)
        if (!this._lastCursorUpdate || Date.now() - this._lastCursorUpdate > 50) {
            const pointer = this.canvas.getPointer(e);
            this._notifyCursorPosition(Math.round(pointer.x), Math.round(pointer.y));
            this._lastCursorUpdate = Date.now();
        }
    },
    
    /**
     * Handle mouse up (Fabric.js event)
     */
    _handleMouseUp(opt) {
        const e = opt.e;
        
        // Skip if middle mouse panning is active (handled by native events)
        if (this._middleMousePanning) {
            return;
        }
        
        if (this.isPanning) {
            this.isPanning = false;
            this.lastPanPoint = null;
            
            // Restore previous drawing mode if it was saved
            if (this._savedDrawingMode !== undefined) {
                this.canvas.isDrawingMode = this._savedDrawingMode;
                this._savedDrawingMode = undefined;
            }
            
            if (this.currentTool === 'pan') {
                this.canvas.defaultCursor = 'grab';
            } else if (!this.isSpacePressed) {
                this.setTool(this.currentTool);
            }
        }
        
        // Reset drawing flag
        if (this._wasDrawing && this.canvas.isDrawingMode) {
            this._wasDrawing = false;
        }
    },
    
    /**
     * Handle path created (after drawing a stroke)
     */
    _handlePathCreated(opt) {
        if (!opt.path) return;
        
        const path = opt.path;
        
        path.set({
            selectable: false,
            evented: false,
            hasControls: false,
            hasBorders: false
        });
        
        let pathName = 'drawing';
        let layerId = this.activeLayerId;
        let layerName = this.getActiveLayer()?.name || 'Drawing Layer';
        const isMaskTool = this.currentTool === 'maskbrush' || this.currentTool === 'maskeraser';
        
        switch (this.currentTool) {
            case 'maskbrush':
                pathName = 'mask';
                layerId = LAYER_MASK;
                layerName = 'Mask';
                path.set('opacity', this.maskOpacity);
                this.maskObjects.push(path);
                this._notifyMaskChanged(true);
                break;
                
            case 'maskeraser':
                this.canvas.remove(path);
                this._eraseMaskAtPath(path);
                this._afterPathCreated();
                return;
                
            case 'eraser':
                this.canvas.remove(path);
                this._eraseDrawingAtPath(path);
                this._afterPathCreated();
                return;
                
            default:
                pathName = 'drawing';
                break;
        }
        
        // Set layer properties
        path.name = pathName;
        path.layerId = layerId;
        path.layer = layerName;
        
        const originalToObject = path.toObject.bind(path);
        path.toObject = function(propertiesToInclude) {
            const obj = originalToObject(propertiesToInclude);
            obj.name = this.name;
            obj.layerId = this.layerId;
            obj.layer = this.layer;
            return obj;
        };
        
        // Reorder canvas objects to maintain layer order
        try {
            this._reorderCanvasObjects();
        } catch (err) {
            console.warn('Error reordering canvas objects:', err);
        }
        
        if (isMaskTool) {
            this._notifyLayerChanged();
        }
        
        this._afterPathCreated();
        
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCanvasModified');
        }
        
        // Ensure brush is properly configured
        if (this.canvas.isDrawingMode && this.canvas.freeDrawingBrush) {
            this._updateBrushSize();
        }
    },
    
    /**
     * Erase drawing at path
     */
    _eraseDrawingAtPath(eraserPath) {
        const eraserBounds = eraserPath.getBoundingRect();
        
        const drawingObjects = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing' && obj.layerId === this.activeLayerId
        );
        const toRemove = [];
        
        drawingObjects.forEach(drawingObj => {
            const drawingBounds = drawingObj.getBoundingRect();
            if (boundsIntersect(eraserBounds, drawingBounds)) {
                toRemove.push(drawingObj);
            }
        });
        
        toRemove.forEach(obj => {
            this.canvas.remove(obj);
        });
        
        this.canvas.renderAll();
        
        if (toRemove.length > 0 && this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCanvasModified');
        }
    },
    
    /**
     * Erase mask at path
     */
    _eraseMaskAtPath(eraserPath) {
        const eraserBounds = eraserPath.getBoundingRect();
        
        const toRemove = [];
        this.maskObjects.forEach(maskObj => {
            const maskBounds = maskObj.getBoundingRect();
            if (boundsIntersect(eraserBounds, maskBounds)) {
                toRemove.push(maskObj);
            }
        });
        
        toRemove.forEach(obj => {
            this.canvas.remove(obj);
            const idx = this.maskObjects.indexOf(obj);
            if (idx > -1) {
                this.maskObjects.splice(idx, 1);
            }
        });
        
        if (this.maskObjects.length === 0) {
            this._notifyMaskChanged(false);
        }
        
        this.canvas.renderAll();
    },
    
    /**
     * Handle key down
     */
    _handleKeyDown(e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        
        if (e.code === 'Space' && !this.isSpacePressed) {
            this.isSpacePressed = true;
            if (this.currentTool !== 'pan') {
                this.canvas.isDrawingMode = false;
                this.canvas.defaultCursor = 'grab';
                this._hideBrushCursor();
            }
            e.preventDefault();
        }
        
        if (e.ctrlKey && e.code === 'KeyZ' && !e.shiftKey) {
            this.undo();
            e.preventDefault();
        }
        
        if ((e.ctrlKey && e.code === 'KeyY') || (e.ctrlKey && e.shiftKey && e.code === 'KeyZ')) {
            this.redo();
            e.preventDefault();
        }
        
        if ((e.code === 'Delete' || e.code === 'Backspace') && this.currentTool === 'select') {
            this._deleteSelectedObjects();
            e.preventDefault();
        }
        
        if (!e.ctrlKey && !e.altKey) {
            switch (e.code) {
                case 'KeyB':
                    this._notifyToolChange('brush');
                    break;
                case 'KeyE':
                    this._notifyToolChange('eraser');
                    break;
                case 'KeyM':
                    this._notifyToolChange('maskbrush');
                    break;
                case 'KeyG':
                    this._notifyToolChange('fill');
                    break;
                case 'KeyI':
                    this._notifyToolChange('colorpicker');
                    break;
                case 'KeyV':
                    this._notifyToolChange('select');
                    break;
                case 'BracketLeft':
                    this._notifyBrushSizeChange(-5);
                    break;
                case 'BracketRight':
                    this._notifyBrushSizeChange(5);
                    break;
            }
        }
        
        if (e.ctrlKey && e.code === 'Digit0') {
            this.fitToView();
            e.preventDefault();
        }
        
        if (e.ctrlKey && e.code === 'Digit1') {
            this.zoomToActual();
            e.preventDefault();
        }
    },
    
    /**
     * Handle key up
     */
    _handleKeyUp(e) {
        if (e.code === 'Space') {
            this.isSpacePressed = false;
            if (!this.isPanning) {
                this.setTool(this.currentTool);
            }
        }
    },
    
    /**
     * Handle resize
     */
    _handleResize() {
        // Will be called from Blazor with new dimensions
    },
    
    /**
     * Delete selected objects
     */
    _deleteSelectedObjects() {
        const activeObjects = this.canvas.getActiveObjects();
        if (activeObjects.length === 0) return;
        
        this._saveStateBeforeAction();
        
        activeObjects.forEach(obj => {
            if (obj.name === 'baseImage') return;
            
            const layer = this.layers.find(l => l.id === obj.layerId);
            if (layer && layer.locked) return;
            
            this.canvas.remove(obj);
        });
        
        this.canvas.discardActiveObject();
        this.canvas.renderAll();
        
        this._afterPathCreated();
        
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCanvasModified');
        }
    },
    
    /**
     * Cleanup event listeners (called from dispose)
     */
    _cleanupEventListeners() {
        document.removeEventListener('keydown', this._boundKeyDown);
        document.removeEventListener('keyup', this._boundKeyUp);
        window.removeEventListener('resize', this._boundResize);
        
        // Cleanup native mouse handlers
        if (this._boundNativeMouseMove) {
            document.removeEventListener('mousemove', this._boundNativeMouseMove);
        }
        if (this._boundNativeMouseUp) {
            document.removeEventListener('mouseup', this._boundNativeMouseUp);
        }
    }
};
