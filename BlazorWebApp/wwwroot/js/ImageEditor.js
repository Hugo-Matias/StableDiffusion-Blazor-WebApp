/**
 * ImageEditor.js - Fabric.js-based image editor module
 * Provides canvas manipulation, drawing tools, zoom/pan, and history management
 */

let fabricLoaded = typeof fabric !== 'undefined';
let fabricLoadPromise = null;

const FABRIC_CDNS = [
    'https://cdn.jsdelivr.net/npm/fabric@6.0.2/dist/index.min.js',
    'https://unpkg.com/fabric@6.0.2/dist/index.min.js',
    'https://cdnjs.cloudflare.com/ajax/libs/fabric.js/6.0.2/fabric.min.js'
];

function loadScript(url) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = url;
        script.crossOrigin = 'anonymous';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load script: ${url}`));
        document.head.appendChild(script);
    });
}

async function ensureFabricLoaded() {
    if (fabricLoaded || typeof fabric !== 'undefined') {
        fabricLoaded = true;
        return;
    }
    
    if (fabricLoadPromise) {
        return fabricLoadPromise;
    }
    
    fabricLoadPromise = (async () => {
        for (const cdn of FABRIC_CDNS) {
            try {
                console.log(`Attempting to load Fabric.js from: ${cdn}`);
                await loadScript(cdn);
                
                if (typeof fabric !== 'undefined') {
                    fabricLoaded = true;
                    console.log('Fabric.js loaded successfully from:', cdn);
                    return;
                }
            } catch (err) {
                console.warn(`Failed to load from ${cdn}:`, err.message);
            }
        }
        
        throw new Error('Failed to load Fabric.js from all CDN sources');
    })();
    
    return fabricLoadPromise;
}

export async function init(canvasId, dotNetRef, options = {}) {
    await ensureFabricLoaded();
    
    const canvasElement = document.getElementById(canvasId);
    if (!canvasElement) {
        throw new Error(`Canvas element with id '${canvasId}' not found`);
    }
    
    const parent = canvasElement.parentElement;
    if (parent && parent.classList.contains('canvas-container')) {
        throw new Error('Canvas element is already wrapped by Fabric.js. Ensure proper disposal.');
    }
    
    return new ImageEditor(canvasId, dotNetRef, options);
}

class ImageEditor {
    constructor(canvasId, dotNetRef, options = {}) {
        this.canvasId = canvasId;
        this.dotNetRef = dotNetRef;
        this.options = {
            maxCanvasSize: 4096,
            maxHistorySize: 50,
            defaultBrushSize: 20,
            defaultBrushColor: '#FFFFFF',
            maskColor: '#FF0000',
            maskOpacity: 0.5,
            ...options
        };
        
        // State
        this.isInitialized = false;
        this.isPanning = false;
        this.isSpacePressed = false;
        this.lastPanPoint = null;
        this.currentTool = 'brush';
        this.previousTool = 'brush';
        this.historyLocked = false;
        this._baseBrushSize = this.options.defaultBrushSize;
        this._currentBrushColor = this.options.defaultBrushColor;
        
        // Zoom debounce state
        this._lastZoomNotify = 0;
        this._zoomNotifyTimeout = null;
        this._isSettingZoomFromExternal = false;
        
        // Layer references
        this.baseImageObject = null;
        this.maskObjects = [];
        
        // Mask settings
        this.maskVisible = true;
        this.maskOpacity = this.options.maskOpacity;
        this.maskColor = this.options.maskColor;
        
        // Brush cursor
        this._brushCursorOuter = null;
        this._brushCursorInner = null;
        this._cursorVisible = false;
        
        // Bound event handlers for cleanup
        this._boundKeyDown = this._handleKeyDown.bind(this);
        this._boundKeyUp = this._handleKeyUp.bind(this);
        this._boundResize = this._handleResize.bind(this);
        this._boundPreventMiddleScroll = this._preventMiddleScroll.bind(this);
        
        // Initialize canvas
        this._initCanvas();
        this._setupEventListeners();
        this._createBrushCursor();
        
        this.isInitialized = true;
        console.log('ImageEditor initialized for canvas:', canvasId);
    }
    
    // ==================== Initialization ====================
    
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
    }
    
    _setupBrush(toolType) {
        this.canvas.freeDrawingBrush = new fabric.PencilBrush(this.canvas);
        this.canvas.freeDrawingBrush.strokeLineCap = 'round';
        this.canvas.freeDrawingBrush.strokeLineJoin = 'round';
        
        const zoom = this.canvas.getZoom() || 1;
        this.canvas.freeDrawingBrush.width = this._baseBrushSize / zoom;
        
        switch (toolType) {
            case 'eraser':
                // Eraser: use destination-out to actually erase
                this.canvas.freeDrawingBrush.color = 'rgba(255,255,255,1)';
                // We'll handle eraser in path:created by setting globalCompositeOperation
                break;
            case 'maskbrush':
                // Mask brush: solid color, opacity applied at layer level
                this.canvas.freeDrawingBrush.color = this.maskColor;
                break;
            case 'maskeraser':
                this.canvas.freeDrawingBrush.color = '#000000';
                break;
            default:
                this.canvas.freeDrawingBrush.color = this._currentBrushColor;
                break;
        }
    }
    
    _createBrushCursor() {
        // Create two circles for brush cursor visualization - black and white for visibility on all backgrounds
        // Don't use a group - just use individual circles positioned at the same spot
        this._brushCursorOuter = new fabric.Circle({
            radius: this._baseBrushSize / 2,
            fill: 'transparent',
            stroke: 'rgba(0, 0, 0, 0.8)',
            strokeWidth: 1,
            originX: 'center',
            originY: 'center',
            left: 0,
            top: 0,
            selectable: false,
            evented: false,
            excludeFromExport: true,
            name: '_brushCursor'
        });
        
        this._brushCursorInner = new fabric.Circle({
            radius: this._baseBrushSize / 2,
            fill: 'transparent',
            stroke: 'rgba(255, 255, 255, 0.8)',
            strokeWidth: 1,
            strokeDashArray: [4, 4],
            originX: 'center',
            originY: 'center',
            left: 0,
            top: 0,
            selectable: false,
            evented: false,
            excludeFromExport: true,
            name: '_brushCursor'
        });
    }
    
    _updateBrushCursor(x, y) {
        if (!this._brushCursorOuter || !this._brushCursorInner) return;
        
        const zoom = this.canvas.getZoom();
        const radius = (this._baseBrushSize / 2) / zoom;
        const strokeWidth = Math.max(1 / zoom, 0.5);
        
        // Update outer circle (black)
        this._brushCursorOuter.set({
            left: x,
            top: y,
            radius: radius,
            strokeWidth: strokeWidth
        });
        
        // Update inner circle (white dashed)
        this._brushCursorInner.set({
            left: x,
            top: y,
            radius: radius,
            strokeWidth: strokeWidth
        });
        
        // Add circles if not visible
        if (!this._cursorVisible && this.canvas.isDrawingMode) {
            this.canvas.add(this._brushCursorOuter);
            this.canvas.add(this._brushCursorInner);
            this._cursorVisible = true;
        }
        
        // Ensure cursors are on top
        this._brushCursorOuter.bringToFront?.() || this.canvas.bringToFront?.(this._brushCursorOuter);
        this._brushCursorInner.bringToFront?.() || this.canvas.bringToFront?.(this._brushCursorInner);
        
        this.canvas.renderAll();
    }
    
    _hideBrushCursor() {
        if (this._cursorVisible) {
            if (this._brushCursorOuter) {
                this.canvas.remove(this._brushCursorOuter);
            }
            if (this._brushCursorInner) {
                this.canvas.remove(this._brushCursorInner);
            }
            this._cursorVisible = false;
            this.canvas.renderAll();
        }
    }
    
    _setupEventListeners() {
        this.canvas.on('mouse:wheel', (opt) => this._handleMouseWheel(opt));
        this.canvas.on('mouse:down', (opt) => this._handleMouseDown(opt));
        this.canvas.on('mouse:move', (opt) => this._handleMouseMove(opt));
        this.canvas.on('mouse:up', (opt) => this._handleMouseUp(opt));
        this.canvas.on('mouse:out', () => this._hideBrushCursor());
        this.canvas.on('path:created', (opt) => this._handlePathCreated(opt));
        
        document.addEventListener('keydown', this._boundKeyDown);
        document.addEventListener('keyup', this._boundKeyUp);
        window.addEventListener('resize', this._boundResize);
        
        // Prevent middle mouse button default scroll behavior
        const wrapper = document.getElementById(this.canvasId)?.parentElement;
        if (wrapper) {
            wrapper.addEventListener('mousedown', this._boundPreventMiddleScroll);
        }
    }
    
    _preventMiddleScroll(e) {
        if (e.button === 1) {
            e.preventDefault();
            e.stopPropagation();
        }
    }
    
    // ==================== Canvas Management ====================
    
    resize(width, height) {
        if (!this.canvas) return;
        
        console.log('Resizing canvas to:', width, 'x', height);
        
        this.canvas.setWidth(width);
        this.canvas.setHeight(height);
        this.canvas.renderAll();
        
        if (this.baseImageObject) {
            this.fitToView();
        }
    }
    
    async loadImage(imageData) {
        if (!imageData) {
            throw new Error('No image data provided');
        }
        
        console.log('Loading image, data length:', imageData.length);
        
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
            
            console.log('Image object created, dimensions:', img.width, 'x', img.height);
            
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
                hasBorders: false,
                name: 'baseImage'
            });
            
            this.baseImageObject = img;
            this.canvas.add(img);
            this._sendToBack(img);
            
            console.log('Image added to canvas');
            this.fitToView();
            this._notifyImageLoaded(img.width, img.height);
            
            // Save initial state for undo (so first action can be undone)
            this._saveHistoryState();
            
        } catch (error) {
            console.error('Error loading image:', error);
            throw error;
        }
    }
    
    createBlankCanvas(width, height, backgroundColor = '#FFFFFF') {
        console.log('Creating blank canvas:', width, 'x', height);
        
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
            hasBorders: false,
            name: 'baseImage'
        });
        
        this.baseImageObject = background;
        this.canvas.add(background);
        this._sendToBack(background);
        
        this.fitToView();
        this._notifyImageLoaded(width, height);
        
        // Save initial state for undo (so first action can be undone)
        this._saveHistoryState();
    }
    
    _sendToBack(obj) {
        if (typeof this.canvas.sendObjectToBack === 'function') {
            this.canvas.sendObjectToBack(obj);
        } else if (typeof obj.sendToBack === 'function') {
            obj.sendToBack();
        } else {
            this.canvas.moveTo(obj, 0);
        }
    }
    
    clearDrawing() {
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing'
        );
        
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        this.canvas.renderAll();
        this._saveHistoryState();
    }
    
    clearMask() {
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'mask'
        );
        
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        this.maskObjects = [];
        this.canvas.renderAll();
        this._saveHistoryState();
        this._notifyMaskChanged(false);
    }
    
    clearAll() {
        this.canvas.clear();
        this.canvas.backgroundColor = '#1a1a1a';
        this.baseImageObject = null;
        this.maskObjects = [];
        this.canvas.renderAll();
    }
    
    // ==================== Zoom & Pan ====================
    
    /**
     * Set zoom level - called from .NET when slider changes
     * Uses flag to prevent feedback loop
     */
    setZoom(zoom, point = null) {
        // Prevent feedback loop when called from .NET
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
        
        // Reset flag after a short delay
        setTimeout(() => {
            this._isSettingZoomFromExternal = false;
        }, 50);
    }
    
    getZoom() {
        return this.canvas.getZoom();
    }
    
    fitToView() {
        if (!this.baseImageObject) return;
        
        this._isSettingZoomFromExternal = true;
        
        const canvasWidth = this.canvas.getWidth();
        const canvasHeight = this.canvas.getHeight();
        const imgWidth = this.baseImageObject.width;
        const imgHeight = this.baseImageObject.height;
        
        const padding = 40;
        const scaleX = (canvasWidth - padding * 2) / imgWidth;
        const scaleY = (canvasHeight - padding * 2) / imgHeight;
        const scale = Math.min(scaleX, scaleY, 1);
        
        this.canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
        
        const offsetX = (canvasWidth - imgWidth * scale) / 2;
        const offsetY = (canvasHeight - imgHeight * scale) / 2;
        
        this.canvas.setViewportTransform([scale, 0, 0, scale, offsetX, offsetY]);
        this.canvas.renderAll();
        
        this._updateBrushSize();
        this._notifyZoomChangedDebounced(scale);
        
        setTimeout(() => {
            this._isSettingZoomFromExternal = false;
        }, 50);
    }
    
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
        
        const offsetX = (canvasWidth - imgWidth) / 2;
        const offsetY = (canvasHeight - imgHeight) / 2;
        
        this.canvas.setViewportTransform([1, 0, 0, 1, offsetX, offsetY]);
        this.canvas.renderAll();
        
        this._updateBrushSize();
        this._notifyZoomChangedDebounced(1);
        
        setTimeout(() => {
            this._isSettingZoomFromExternal = false;
        }, 50);
    }
    
    pan(deltaX, deltaY) {
        const vpt = this.canvas.viewportTransform;
        vpt[4] += deltaX;
        vpt[5] += deltaY;
        this.canvas.setViewportTransform(vpt);
        this.canvas.renderAll();
    }
    
    // ==================== Tools ====================
    
    setTool(tool) {
        this.previousTool = this.currentTool;
        this.currentTool = tool;
        
        // Disable object selection for all tools
        this.canvas.selection = false;
        this.canvas.forEachObject(obj => {
            if (obj.name !== 'baseImage') {
                obj.selectable = false;
                obj.evented = false;
            }
        });
        
        switch (tool) {
            case 'brush':
                this.canvas.isDrawingMode = true;
                this.canvas.defaultCursor = 'none'; // Hide cursor, we show brush circle
                this._setupBrush('brush');
                break;
                
            case 'eraser':
                this.canvas.isDrawingMode = true;
                this.canvas.defaultCursor = 'none';
                this._setupBrush('eraser');
                break;
                
            case 'maskbrush':
                this.canvas.isDrawingMode = true;
                this.canvas.defaultCursor = 'none';
                this._setupBrush('maskbrush');
                break;
                
            case 'maskeraser':
                this.canvas.isDrawingMode = true;
                this.canvas.defaultCursor = 'none';
                this._setupBrush('maskeraser');
                break;
                
            case 'pan':
                this.canvas.isDrawingMode = false;
                this.canvas.defaultCursor = 'grab';
                this._hideBrushCursor();
                break;
                
            case 'colorpicker':
                this.canvas.isDrawingMode = false;
                this.canvas.defaultCursor = 'crosshair';
                this._hideBrushCursor();
                break;
                
            case 'fill':
                this.canvas.isDrawingMode = false;
                this.canvas.defaultCursor = 'crosshair';
                this._hideBrushCursor();
                break;
                
            default:
                this.canvas.isDrawingMode = true;
                this._setupBrush('brush');
        }
    }
    
    setBrush(props) {
        if (props.color !== undefined) {
            this._currentBrushColor = props.color;
            if (this.currentTool === 'brush' && this.canvas.freeDrawingBrush) {
                this.canvas.freeDrawingBrush.color = props.color;
            }
        }
        if (props.size !== undefined) {
            this._baseBrushSize = props.size;
            this._updateBrushSize();
        }
        if (props.opacity !== undefined && this.canvas.freeDrawingBrush) {
            const color = this._currentBrushColor;
            this.canvas.freeDrawingBrush.color = this._colorWithOpacity(color, props.opacity);
        }
    }
    
    setMaskSettings(props) {
        if (props.color !== undefined) {
            this.maskColor = props.color;
        }
        if (props.opacity !== undefined) {
            this.maskOpacity = props.opacity;
            this.maskObjects.forEach(obj => {
                obj.set('opacity', props.opacity);
            });
            this.canvas.renderAll();
        }
        if (props.visible !== undefined) {
            this.maskVisible = props.visible;
            this.maskObjects.forEach(obj => {
                obj.set('visible', props.visible);
            });
            this.canvas.renderAll();
        }
        
        if (this.currentTool === 'maskbrush') {
            this._setupBrush('maskbrush');
        }
    }
    
    getColorAtPoint(x, y) {
        const ctx = this.canvas.getContext();
        const pixel = ctx.getImageData(x, y, 1, 1).data;
        return this._rgbToHex(pixel[0], pixel[1], pixel[2]);
    }
    
    // ==================== History ====================
    
    undo() {
        this._notifyRequestUndo();
    }
    
    redo() {
        this._notifyRequestRedo();
    }
    
    saveState() {
        this._saveHistoryState();
    }
    
    loadState(json) {
        if (!json) return;
        
        this.historyLocked = true;
        
        // Store current viewport transform
        const currentVPT = [...this.canvas.viewportTransform];
        
        // Parse the state to get objects
        let stateData;
        try {
            stateData = JSON.parse(json);
        } catch (e) {
            console.error('Failed to parse state JSON:', e);
            this.historyLocked = false;
            return;
        }
        
        // Remove all non-base objects (drawings, masks) without clearing canvas
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing' || obj.name === 'mask' || obj.name === '_brushCursor'
        );
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        
        // Clear mask objects array
        this.maskObjects = [];
        
        // Get objects to restore from saved state
        const objectsToRestore = stateData.objects?.filter(
            objData => objData.name === 'drawing' || objData.name === 'mask'
        ) || [];
        
        console.log('loadState: restoring', objectsToRestore.length, 'objects');
        
        if (objectsToRestore.length === 0) {
            // No objects to restore, just render
            this.canvas.setViewportTransform(currentVPT);
            this.canvas.renderAll();
            this.historyLocked = false;
            this._notifyMaskChanged(false);
            return;
        }
        
        // Use fabric.util.enlivenObjects - check both Fabric 5 and 6 APIs
        const enlivenFn = fabric.util.enlivenObjects;
        
        // Fabric 6 uses promises, Fabric 5 uses callbacks
        const enlivenResult = enlivenFn(objectsToRestore);
        
        const handleEnlivenedObjects = (enlivenedObjects) => {
            if (enlivenedObjects && enlivenedObjects.length > 0) {
                enlivenedObjects.forEach(obj => {
                    obj.set({
                        selectable: false,
                        evented: false,
                        hasControls: false,
                        hasBorders: false
                    });
                    this.canvas.add(obj);
                    
                    if (obj.name === 'mask') {
                        this.maskObjects.push(obj);
                    }
                });
            }
            
            // Ensure viewport is preserved
            this.canvas.setViewportTransform(currentVPT);
            this.canvas.renderAll();
            this.historyLocked = false;
            
            // Update mask state
            this._notifyMaskChanged(this.maskObjects.length > 0);
        };
        
        if (enlivenResult instanceof Promise) {
            // Fabric 6 API
            enlivenResult.then(handleEnlivenedObjects).catch(err => {
                console.error('Failed to enliven objects:', err);
                this.canvas.setViewportTransform(currentVPT);
                this.canvas.renderAll();
                this.historyLocked = false;
            });
        } else {
            // Fabric 5 API (callback-based) - this shouldn't happen with v6
            // but handle it just in case
            handleEnlivenedObjects(enlivenResult);
        }
    }
    
    getState() {
        return JSON.stringify(this.canvas.toJSON(['name']));
    }
    
    // ==================== Export ====================
    
    exportImage(includeBackground = true) {
        console.log('Exporting image, baseImageObject:', this.baseImageObject ? 'exists' : 'null');
        
        if (!this.baseImageObject) {
            console.warn('No base image to export');
            return null;
        }
        
        // Hide brush cursor during export
        this._hideBrushCursor();
        
        const bounds = {
            left: 0,
            top: 0,
            width: this.baseImageObject.width,
            height: this.baseImageObject.height
        };
        
        console.log('Export bounds:', bounds);
        
        // Temporarily hide mask layer for image export
        const maskWasVisible = this.maskVisible;
        if (maskWasVisible) {
            this.maskObjects.forEach(obj => obj.set('visible', false));
            this.canvas.renderAll();
        }
        
        const currentVPT = [...this.canvas.viewportTransform];
        this.canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
        
        try {
            const dataUrl = this.canvas.toDataURL({
                format: 'png',
                left: bounds.left,
                top: bounds.top,
                width: bounds.width,
                height: bounds.height,
                multiplier: 1
            });
            
            console.log('Exported image, data URL length:', dataUrl ? dataUrl.length : 0);
            
            this.canvas.setViewportTransform(currentVPT);
            
            if (maskWasVisible) {
                this.maskObjects.forEach(obj => obj.set('visible', true));
                this.canvas.renderAll();
            }
            
            return dataUrl;
        } catch (error) {
            console.error('Error exporting image:', error);
            this.canvas.setViewportTransform(currentVPT);
            if (maskWasVisible) {
                this.maskObjects.forEach(obj => obj.set('visible', true));
                this.canvas.renderAll();
            }
            throw error;
        }
    }
    
    exportMask() {
        if (!this.baseImageObject) {
            console.warn('No base image for mask export');
            return null;
        }
        
        const maskObjs = this.canvas.getObjects().filter(obj => obj.name === 'mask');
        if (maskObjs.length === 0) {
            console.log('No mask objects to export');
            return null;
        }
        
        console.log('Exporting mask with', maskObjs.length, 'objects');
        
        // Create a temporary canvas for mask export
        const tempCanvas = document.createElement('canvas');
        tempCanvas.width = this.baseImageObject.width;
        tempCanvas.height = this.baseImageObject.height;
        const ctx = tempCanvas.getContext('2d');
        
        // Fill with black (keep areas)
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, tempCanvas.width, tempCanvas.height);
        
        // Draw mask objects in white (inpaint areas)
        ctx.fillStyle = '#FFFFFF';
        ctx.strokeStyle = '#FFFFFF';
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        
        maskObjs.forEach(obj => {
            if (obj.path && Array.isArray(obj.path)) {
                ctx.beginPath();
                ctx.lineWidth = obj.strokeWidth || this._baseBrushSize;
                
                obj.path.forEach((cmd, i) => {
                    if (cmd[0] === 'M') {
                        ctx.moveTo(cmd[1], cmd[2]);
                    } else if (cmd[0] === 'Q') {
                        ctx.quadraticCurveTo(cmd[1], cmd[2], cmd[3], cmd[4]);
                    } else if (cmd[0] === 'L') {
                        ctx.lineTo(cmd[1], cmd[2]);
                    }
                });
                
                ctx.stroke();
            }
        });
        
        return tempCanvas.toDataURL('image/png');
    }
    
    // ==================== Event Handlers ====================
    
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
    }
    
    _handleMouseDown(opt) {
        const e = opt.e;
        
        // Middle mouse button for panning
        if (e.button === 1) {
            e.preventDefault();
            e.stopPropagation();
            this.isPanning = true;
            this.lastPanPoint = { x: e.clientX, y: e.clientY };
            this.canvas.isDrawingMode = false;
            this.canvas.defaultCursor = 'grabbing';
            this._hideBrushCursor();
            return;
        }
        
        // Space + left click for panning
        if (this.isSpacePressed && e.button === 0) {
            e.preventDefault();
            this.isPanning = true;
            this.lastPanPoint = { x: e.clientX, y: e.clientY };
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
        }
    }
    
    _handleMouseMove(opt) {
        const e = opt.e;
        
        // Update brush cursor position
        if (this.canvas.isDrawingMode && !this.isPanning) {
            // Convert screen coordinates to canvas coordinates
            const pointer = this.canvas.getPointer(e);
            this._updateBrushCursor(pointer.x, pointer.y);
        }
        
        // Handle panning
        if (this.isPanning && this.lastPanPoint) {
            const deltaX = e.clientX - this.lastPanPoint.x;
            const deltaY = e.clientY - this.lastPanPoint.y;
            this.pan(deltaX, deltaY);
            this.lastPanPoint = { x: e.clientX, y: e.clientY };
            return;
        }
        
        // Update cursor position for .NET (throttled)
        if (!this._lastCursorUpdate || Date.now() - this._lastCursorUpdate > 50) {
            const pointer = this.canvas.getPointer(e);
            this._notifyCursorPosition(Math.round(pointer.x), Math.round(pointer.y));
            this._lastCursorUpdate = Date.now();
        }
    }
    
    _handleMouseUp(opt) {
        if (this.isPanning) {
            this.isPanning = false;
            this.lastPanPoint = null;
            
            // Restore tool state
            if (this.currentTool === 'pan') {
                this.canvas.defaultCursor = 'grab';
            } else if (!this.isSpacePressed) {
                this.setTool(this.currentTool);
            }
        }
    }
    
    _handlePathCreated(opt) {
        if (!opt.path) return;
        
        const path = opt.path;
        
        // Disable selection on the new path
        path.set({
            selectable: false,
            evented: false,
            hasControls: false,
            hasBorders: false
        });
        
        switch (this.currentTool) {
            case 'maskbrush':
                path.name = 'mask';
                path.set('opacity', this.maskOpacity);
                this.maskObjects.push(path);
                this._notifyMaskChanged(true);
                break;
                
            case 'maskeraser':
                // Find and remove mask paths that this stroke overlaps
                this.canvas.remove(path); // Remove the eraser stroke
                this._eraseMaskAtPath(path);
                break;
                
            case 'eraser':
                // Find and remove drawing paths that this stroke overlaps
                path.name = 'eraser';
                // For simplicity, eraser draws white on the canvas
                // A more sophisticated approach would use clipping or destination-out
                path.name = 'drawing';
                break;
                
            default:
                path.name = 'drawing';
                break;
        }
        
        if (!this.historyLocked) {
            this._saveHistoryState();
        }
    }
    
    _eraseMaskAtPath(eraserPath) {
        // Simple approach: remove mask objects that intersect with eraser path
        // This is a basic implementation - a more sophisticated one would clip paths
        const eraserBounds = eraserPath.getBoundingRect();
        
        const toRemove = [];
        this.maskObjects.forEach(maskObj => {
            const maskBounds = maskObj.getBoundingRect();
            
            // Check if bounding boxes intersect
            if (this._boundsIntersect(eraserBounds, maskBounds)) {
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
    }
    
    _boundsIntersect(a, b) {
        return !(a.left > b.left + b.width ||
                 a.left + a.width < b.left ||
                 a.top > b.top + b.height ||
                 a.top + a.height < b.top);
    }
    
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
    }
    
    _handleKeyUp(e) {
        if (e.code === 'Space') {
            this.isSpacePressed = false;
            if (!this.isPanning) {
                this.setTool(this.currentTool);
            }
        }
    }
    
    _handleResize() {
        // Will be called from Blazor with new dimensions
    }
    
    // ==================== Helper Methods ====================
    
    _updateBrushSize() {
        if (this.canvas.freeDrawingBrush && this._baseBrushSize) {
            const zoom = this.canvas.getZoom();
            this.canvas.freeDrawingBrush.width = this._baseBrushSize / zoom;
        }
    }
    
    _colorWithOpacity(hexColor, opacity) {
        if (!hexColor) return hexColor;
        if (hexColor.startsWith('rgba')) return hexColor;
        
        if (hexColor.startsWith('rgb(')) {
            const match = hexColor.match(/rgb\((\d+),\s*(\d+),\s*(\d+)\)/);
            if (match) {
                return `rgba(${match[1]}, ${match[2]}, ${match[3]}, ${opacity})`;
            }
        }
        
        if (hexColor.startsWith('#')) {
            const r = parseInt(hexColor.slice(1, 3), 16);
            const g = parseInt(hexColor.slice(3, 5), 16);
            const b = parseInt(hexColor.slice(5, 7), 16);
            return `rgba(${r}, ${g}, ${b}, ${opacity})`;
        }
        
        return hexColor;
    }
    
    _rgbToHex(r, g, b) {
        return '#' + [r, g, b].map(x => {
            const hex = x.toString(16);
            return hex.length === 1 ? '0' + hex : hex;
        }).join('');
    }
    
    _saveHistoryState() {
        if (this.historyLocked) return;
        
        const state = this.getState();
        this._notifySaveState(state);
    }
    
    // ==================== .NET Callbacks ====================
    
    _notifyImageLoaded(width, height) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnImageLoaded', width, height);
        }
    }
    
    /**
     * Debounced zoom notification to prevent feedback loops
     */
    _notifyZoomChangedDebounced(zoom) {
        // Skip if this is from an external setZoom call
        if (this._isSettingZoomFromExternal) {
            return;
        }
        
        // Clear any pending timeout
        if (this._zoomNotifyTimeout) {
            clearTimeout(this._zoomNotifyTimeout);
        }
        
        // Debounce: only notify after 100ms of no changes
        this._zoomNotifyTimeout = setTimeout(() => {
            this._notifyZoomChanged(zoom);
        }, 100);
    }
    
    _notifyZoomChanged(zoom) {
        if (this.dotNetRef && !this._isSettingZoomFromExternal) {
            this.dotNetRef.invokeMethodAsync('OnZoomChanged', zoom);
        }
    }
    
    _notifyColorPicked(color) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnColorPicked', color);
        }
    }
    
    _notifyToolChange(tool) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnToolChangeRequested', tool);
        }
    }
    
    _notifyBrushSizeChange(delta) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnBrushSizeChangeRequested', delta);
        }
    }
    
    _notifyCursorPosition(x, y) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCursorPositionChanged', x, y);
        }
    }
    
    _notifySaveState(state) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnSaveState', state);
        }
    }
    
    _notifyRequestUndo() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnUndoRequested');
        }
    }
    
    _notifyRequestRedo() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnRedoRequested');
        }
    }
    
    _notifyMaskChanged(hasMask) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnMaskChanged', hasMask);
        }
    }
    
    // ==================== Cleanup ====================
    
    dispose() {
        console.log('Disposing ImageEditor...');
        
        document.removeEventListener('keydown', this._boundKeyDown);
        document.removeEventListener('keyup', this._boundKeyUp);
        window.removeEventListener('resize', this._boundResize);
        
        const wrapper = document.getElementById(this.canvasId)?.parentElement;
        if (wrapper) {
            wrapper.removeEventListener('mousedown', this._boundPreventMiddleScroll);
        }
        
        if (this.canvas) {
            this.canvas.dispose();
            this.canvas = null;
        }
        
        this.dotNetRef = null;
        this.isInitialized = false;
    }
}
