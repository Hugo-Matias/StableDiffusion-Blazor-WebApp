/**
 * ImageEditor.canvas.js - Canvas initialization and management
 */

import { LAYER_BASE } from './ImageEditor.utils.js';

/**
 * Canvas management mixin for ImageEditor
 */
export const CanvasMixin = {
    
    /**
     * Initialize the Fabric.js canvas
     */
    _initCanvas() {
        const element = document.getElementById(this.canvasId);
        if (!element) {
            throw new Error(`Canvas element with id '${this.canvasId}' not found`);
        }
        
        this.canvas = new fabric.Canvas(this.canvasId, {
            isDrawingMode: true,
            backgroundColor: '#1a1a1a',
            selection: false,
            preserveObjectStacking: true,
            enableRetinaScaling: true,
            stopContextMenu: true,
            fireRightClick: true
        });
        
        this._setupBrush('brush');
    },
    
    /**
     * Resize the canvas
     */
    resize(width, height) {
        if (!this.canvas) return;
        
        this.canvas.setWidth(width);
        this.canvas.setHeight(height);
        this.canvas.renderAll();
        
        if (this.baseImageObject) {
            this.fitToView();
        }
    },
    
    /**
     * Load an image into the canvas as the base layer
     */
    async loadImage(imageData) {
        if (!imageData) {
            throw new Error('No image data provided');
        }
        
        try {
            let img;
            const result = fabric.Image.fromURL(imageData, { crossOrigin: 'anonymous' });
            
            if (result instanceof Promise) {
                img = await result;
            } else {
                img = await new Promise((resolve, reject) => {
                    fabric.Image.fromURL(imageData, (loadedImg) => {
                        if (loadedImg) resolve(loadedImg);
                        else reject(new Error('Failed to load image'));
                    }, { crossOrigin: 'anonymous' });
                });
            }
            
            if (!img) throw new Error('Failed to load image - null result');
            
            if (this.baseImageObject) {
                this.canvas.remove(this.baseImageObject);
            }
            
            img.set({
                selectable: false,
                evented: false,
                lockMovementX: true,
                lockMovementY: true,
                lockRotation: true,
                lockScalingX: true,
                lockScalingY: true,
                hasControls: false,
                hasBorders: false
            });
            
            // Set layer properties
            img.name = 'baseImage';
            img.layerId = LAYER_BASE;
            img.layer = 'Base Image';
            
            const originalToObject = img.toObject.bind(img);
            img.toObject = function(propertiesToInclude) {
                const obj = originalToObject(propertiesToInclude);
                obj.name = this.name;
                obj.layerId = this.layerId;
                obj.layer = this.layer;
                return obj;
            };
            
            this.baseImageObject = img;
            this.canvas.add(img);
            this._sendToBack(img);
            
            this.fitToView();
            this._notifyImageLoaded(img.width, img.height);
            
        } catch (error) {
            console.error('Error loading image:', error);
            throw error;
        }
    },
    
    /**
     * Create a blank canvas with specified dimensions
     */
    createBlankCanvas(width, height, backgroundColor = '#FFFFFF') {
        if (this.baseImageObject) {
            this.canvas.remove(this.baseImageObject);
            this.baseImageObject = null;
        }
        
        const background = new fabric.Rect({
            width: width,
            height: height,
            fill: backgroundColor,
            selectable: false,
            evented: false,
            lockMovementX: true,
            lockMovementY: true,
            hasControls: false,
            hasBorders: false
        });
        
        // Set layer properties
        background.name = 'baseImage';
        background.layerId = LAYER_BASE;
        background.layer = 'Base Image';
        
        const originalToObject = background.toObject.bind(background);
        background.toObject = function(propertiesToInclude) {
            const obj = originalToObject(propertiesToInclude);
            obj.name = this.name;
            obj.layerId = this.layerId;
            obj.layer = this.layer;
            return obj;
        };
        
        this.baseImageObject = background;
        this.canvas.add(background);
        this._sendToBack(background);
        
        this.fitToView();
        this._notifyImageLoaded(width, height);
    },
    
    /**
     * Send an object to the back of the canvas
     */
    _sendToBack(obj) {
        if (typeof this.canvas.sendObjectToBack === 'function') {
            this.canvas.sendObjectToBack(obj);
        } else if (typeof obj.sendToBack === 'function') {
            obj.sendToBack();
        } else {
            this.canvas.moveTo(obj, 0);
        }
    },
    
    /**
     * Clear all drawing objects
     */
    clearDrawing() {
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing'
        );
        
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        this.canvas.renderAll();
        this._saveHistoryState();
    },
    
    /**
     * Clear all mask objects
     */
    clearMask() {
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'mask'
        );
        
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        this.maskObjects = [];
        this.canvas.renderAll();
        this._saveHistoryState();
        this._notifyMaskChanged(false);
    },
    
    /**
     * Clear everything from the canvas
     */
    clearAll() {
        this.canvas.clear();
        this.canvas.backgroundColor = '#1a1a1a';
        this.baseImageObject = null;
        this.maskObjects = [];
        this._initializeDefaultLayers();
        this.canvas.renderAll();
    },
    
    // ==================== Zoom & Pan ====================
    
    /**
     * Set zoom level
     */
    setZoom(zoom, point = null) {
        this._isSettingZoomFromExternal = true;
        
        zoom = Math.min(Math.max(zoom, 0.1), 5.0);
        
        if (point) {
            this.canvas.zoomToPoint(new fabric.Point(point.x, point.y), zoom);
        } else {
            const center = this.canvas.getCenter();
            this.canvas.zoomToPoint(new fabric.Point(center.left, center.top), zoom);
        }
        
        this._updateBrushSize();
        this.canvas.renderAll();
        
        setTimeout(() => {
            this._isSettingZoomFromExternal = false;
        }, 50);
    },
    
    /**
     * Get current zoom level
     */
    getZoom() {
        return this.canvas.getZoom();
    },
    
    /**
     * Fit the image to the view
     */
    fitToView() {
        if (!this.baseImageObject) return;
        
        this._isSettingZoomFromExternal = true;
        
        const canvasWidth = this.canvas.getWidth();
        const canvasHeight = this.canvas.getHeight();
        const imgWidth = this.baseImageObject.width;
        const imgHeight = this.baseImageObject.height;
        
        // Account for layer panel width (approximately 220px when expanded)
        const layerPanelOffset = 110;
        
        const padding = 40;
        const availableWidth = canvasWidth - layerPanelOffset;
        const scaleX = (availableWidth - padding * 2) / imgWidth;
        const scaleY = (canvasHeight - padding * 2) / imgHeight;
        const scale = Math.min(scaleX, scaleY, 1);
        
        this.canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
        
        const offsetX = (availableWidth - imgWidth * scale) / 2;
        const offsetY = (canvasHeight - imgHeight * scale) / 2;
        
        this.canvas.setViewportTransform([scale, 0, 0, scale, offsetX, offsetY]);
        this.canvas.renderAll();
        
        this._updateBrushSize();
        this._notifyZoomChangedDebounced(scale);
        
        setTimeout(() => {
            this._isSettingZoomFromExternal = false;
        }, 50);
    },
    
    /**
     * Zoom to 100% (actual size)
     */
    zoomToActual() {
        this._isSettingZoomFromExternal = true;
        
        const canvasWidth = this.canvas.getWidth();
        const canvasHeight = this.canvas.getHeight();
        
        if (!this.baseImageObject) {
            this.canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
            this.canvas.renderAll();
            this._notifyZoomChangedDebounced(1);
            setTimeout(() => { this._isSettingZoomFromExternal = false; }, 50);
            return;
        }
        
        const imgWidth = this.baseImageObject.width;
        const imgHeight = this.baseImageObject.height;
        
        const layerPanelOffset = 110;
        const availableWidth = canvasWidth - layerPanelOffset;
        
        const offsetX = (availableWidth - imgWidth) / 2;
        const offsetY = (canvasHeight - imgHeight) / 2;
        
        this.canvas.setViewportTransform([1, 0, 0, 1, offsetX, offsetY]);
        this.canvas.renderAll();
        
        this._updateBrushSize();
        this._notifyZoomChangedDebounced(1);
        
        setTimeout(() => {
            this._isSettingZoomFromExternal = false;
        }, 50);
    },
    
    /**
     * Pan the canvas
     */
    pan(deltaX, deltaY) {
        const vpt = this.canvas.viewportTransform;
        vpt[4] += deltaX;
        vpt[5] += deltaY;
        this.canvas.setViewportTransform(vpt);
        this.canvas.renderAll();
    }
};
