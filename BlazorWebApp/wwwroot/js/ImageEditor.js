/**
 * ImageEditor.js - Fabric.js-based image editor module
 * Provides canvas manipulation, drawing tools, zoom/pan, history management, and layer support
 */

let fabricLoaded = typeof fabric !== 'undefined';
let fabricLoadPromise = null;

const FABRIC_CDNS = [
    'https://cdn.jsdelivr.net/npm/fabric@6.0.2/dist/index.min.js',
    'https://unpkg.com/fabric@6.0.2/dist/index.min.js',
    'https://cdnjs.cloudflare.com/ajax/libs/fabric.js/6.0.2/fabric.min.js'
];

// Layer constants
const LAYER_BASE = 'base-image-layer';
const LAYER_MASK = 'mask-layer';

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
                await loadScript(cdn);
                
                if (typeof fabric !== 'undefined') {
                    fabricLoaded = true;
                    return;
                }
            } catch (err) {
                console.warn(`Failed to load Fabric.js from ${cdn}`);
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
        
        // History state tracking
        this._stateBeforeActionSaved = false;
        this._pendingUndoState = null;
        
        // Zoom debounce state
        this._lastZoomNotify = 0;
        this._zoomNotifyTimeout = null;
        this._isSettingZoomFromExternal = false;
        
        // Layer references
        this.baseImageObject = null;
        this.maskObjects = [];
        
        // Layer system
        this.layers = [];
        this.activeLayerId = null;
        
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
        this._setupDragDrop();
        this._setupPasteHandler();
        
        // Initialize default layers
        this._initializeDefaultLayers();
        
        this.isInitialized = true;
    }
    
    // ==================== Layer Management ====================
    
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
                id: this._generateId(),
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
    }
    
    _generateId() {
        return 'layer-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
    }
    
    /**
     * Get the currently active layer
     */
    getActiveLayer() {
        return this.layers.find(l => l.id === this.activeLayerId);
    }
    
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
    }
    
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
            id: this._generateId(),
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
    }
    
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
    }
    
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
        if (props.locked !== undefined) layer.locked = props.locked;
        if (props.opacity !== undefined) {
            layer.opacity = props.opacity;
            this._updateLayerOpacity(layerId, props.opacity);
        }
        if (props.order !== undefined) layer.order = props.order;
        
        this._notifyLayerChanged();
        return true;
    }
    
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
    }
    
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
    }
    
    /**
     * Reorder layers
     */
    reorderLayers(layerIds) {
        layerIds.forEach((id, index) => {
            const layer = this.layers.find(l => l.id === id);
            if (layer) {
                layer.order = index;
            }
        });
        
        // Reorder objects on canvas based on layer order
        this._reorderCanvasObjects();
        this._notifyLayerChanged();
    }
    
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
                this.canvas.bringToFront(obj);
            });
        });
        
        // Ensure mask objects are always on top
        this.maskObjects.forEach(obj => {
            this.canvas.bringToFront(obj);
        });
        
        // Brush cursor always on very top
        if (this._brushCursorOuter) this.canvas.bringToFront(this._brushCursorOuter);
        if (this._brushCursorInner) this.canvas.bringToFront(this._brushCursorInner);
        
        this.canvas.renderAll();
    }
    
    /**
     * Get all layers for UI
     */
    getLayers() {
        return this.layers.map(l => ({...l}));
    }
    
    /**
     * Get layer state for serialization
     */
    getLayerState() {
        return {
            layers: this.layers.map(l => ({...l})),
            activeLayerId: this.activeLayerId
        };
    }
    
    /**
     * Restore layer state from serialization
     */
    restoreLayerState(state) {
        if (state && state.layers) {
            this.layers = state.layers;
            this.activeLayerId = state.activeLayerId;
        }
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
                this.canvas.freeDrawingBrush.color = 'rgba(255,255,255,1)';
                break;
            case 'maskbrush':
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
        
        this._brushCursorOuter.set({
            left: x,
            top: y,
            radius: radius,
            strokeWidth: strokeWidth
        });
        
        this._brushCursorInner.set({
            left: x,
            top: y,
            radius: radius,
            strokeWidth: strokeWidth
        });
        
        if (!this._cursorVisible && this.canvas.isDrawingMode) {
            this.canvas.add(this._brushCursorOuter);
            this.canvas.add(this._brushCursorInner);
            this._cursorVisible = true;
        }
        
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
    }
    
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
        this._initializeDefaultLayers();
        this.canvas.renderAll();
    }
    
    // ==================== Zoom & Pan ====================
    
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
        
        // Disable object selection for most tools
        this.canvas.selection = (tool === 'select');
        
        // Update selectability of objects based on tool
        this.canvas.forEachObject(obj => {
            if (obj.name === 'baseImage' || obj.name === '_brushCursor') {
                // Never selectable
                obj.selectable = false;
                obj.evented = false;
            } else if (obj.name === 'mask') {
                // Mask objects are never directly selectable
                obj.selectable = false;
                obj.evented = false;
            } else if (tool === 'select') {
                // In select mode, drawings and imports are selectable (unless layer is locked)
                const layer = this.layers.find(l => l.id === obj.layerId);
                const isLocked = layer ? layer.locked : false;
                obj.selectable = !isLocked;
                obj.evented = !isLocked;
                obj.hasControls = !isLocked;
                obj.hasBorders = !isLocked;
            } else {
                // In other modes, nothing is selectable
                obj.selectable = false;
                obj.evented = false;
            }
        });
        
        switch (tool) {
            case 'brush':
                this.canvas.isDrawingMode = true;
                this.canvas.defaultCursor = 'none';
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
                
            case 'select':
                this.canvas.isDrawingMode = false;
                this.canvas.defaultCursor = 'default';
                this._hideBrushCursor();
                // Deselect any current selection
                this.canvas.discardActiveObject();
                this.canvas.renderAll();
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
    
    /**
     * Save state before an action begins (called on mouse:down in drawing mode)
     * This captures the canvas state BEFORE the new stroke is added
     */
    _saveStateBeforeAction() {
        if (this.historyLocked || this._stateBeforeActionSaved || !this.canvas.isDrawingMode) {
            return;
        }
        
        // Capture the current state (before new stroke is added)
        // This will be sent to .NET after the stroke completes
        this._pendingUndoState = this.getState();
        this._stateBeforeActionSaved = true;
    }
    
    /**
     * Called after path is created to save the captured state and reset the flag
     */
    _afterPathCreated() {
        // Now send the previously captured state to .NET for undo stack
        if (this._pendingUndoState) {
            this._notifySaveState(this._pendingUndoState);
            this._pendingUndoState = null;
        }
        this._stateBeforeActionSaved = false;
    }
    
    _saveHistoryState() {
        if (this.historyLocked) return;
        
        const state = this.getState();
        this._notifySaveState(state);
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
        
        // Get objects to restore from saved state (only drawings and masks)
        const objectsToRestore = stateData.objects?.filter(
            objData => objData.name === 'drawing' || objData.name === 'mask' || objData.name === 'imported'
        ) || [];
        
        // Remove all current drawings and masks
        const objectsToRemove = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing' || obj.name === 'mask' || obj.name === 'imported' || obj.name === '_brushCursor'
        );
        objectsToRemove.forEach(obj => this.canvas.remove(obj));
        
        // Clear mask objects array
        this.maskObjects = [];
        
        if (objectsToRestore.length === 0) {
            // No objects to restore, just render
            this.canvas.setViewportTransform(currentVPT);
            this.canvas.renderAll();
            this.historyLocked = false;
            this._notifyMaskChanged(false);
            return;
        }
        
        // Use direct object creation instead of enlivenObjects to preserve custom properties
        this._restoreObjectsDirectly(objectsToRestore, currentVPT);
    }
    
    _addRestoredObjects(enlivenedObjects, viewportTransform, originalData) {
        if (enlivenedObjects && enlivenedObjects.length > 0) {
            enlivenedObjects.forEach((obj, index) => {
                obj.set({
                    selectable: false,
                    evented: false,
                    hasControls: false,
                    hasBorders: false
                });
                
                // Restore the name from original data since enlivenedObjects doesn't preserve it
                const objData = originalData[index];
                if (objData && objData.name) {
                    obj.name = objData.name;
                    obj.layerId = objData.layerId;
                    obj.layer = objData.layer;
                    
                    // Override toObject to include layer properties
                    const originalToObject = obj.toObject.bind(obj);
                    obj.toObject = function(propertiesToInclude) {
                        const result = originalToObject(propertiesToInclude);
                        result.name = this.name;
                        result.layerId = this.layerId;
                        result.layer = this.layer;
                        return result;
                    };
                }
                
                this.canvas.add(obj);
                
                if (obj.name === 'mask') {
                    this.maskObjects.push(obj);
                }
            });
        }
        
        // Ensure viewport is preserved
        this.canvas.setViewportTransform(viewportTransform);
        this.canvas.renderAll();
        this.historyLocked = false;
        
        // Update mask state
        this._notifyMaskChanged(this.maskObjects.length > 0);
    }
    
    _restoreObjectsDirectly(objectsData, viewportTransform) {
        let restored = 0;
        const imagePromises = [];
        
        objectsData.forEach(objData => {
            try {
                const objType = (objData.type || '').toLowerCase();
                
                // Handle path objects (drawings, masks)
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
                    restored++;
                }
                // Handle image objects (imported images)
                else if (objType === 'image' && objData.src) {
                    const imgPromise = new Promise((resolve) => {
                        const loadImage = async () => {
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
                                    restored++;
                                }
                                resolve();
                            } catch (err) {
                                console.error('Failed to restore image:', err);
                                resolve();
                            }
                        };
                        loadImage();
                    });
                    imagePromises.push(imgPromise);
                }
            } catch (err) {
                console.error('Failed to restore object:', err);
            }
        });
        
        // Wait for all images to load, then finalize
        if (imagePromises.length > 0) {
            Promise.all(imagePromises).then(() => {
                this.canvas.setViewportTransform(viewportTransform);
                this.canvas.renderAll();
                this.historyLocked = false;
                this._notifyMaskChanged(this.maskObjects.length > 0);
            });
        } else {
            this.canvas.setViewportTransform(viewportTransform);
            this.canvas.renderAll();
            this.historyLocked = false;
            this._notifyMaskChanged(this.maskObjects.length > 0);
        }
    }
    
    getState() {
        const state = this.canvas.toJSON(['name', 'layerId', 'layer']);
        return JSON.stringify(state);
    }

    // ==================== Export ====================
    
    exportImage(includeBackground = true) {
        if (!this.baseImageObject) {
            return null;
        }
        
        this._hideBrushCursor();
        
        const bounds = {
            left: 0,
            top: 0,
            width: this.baseImageObject.width,
            height: this.baseImageObject.height
        };
        
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
            return null;
        }
        
        const maskObjs = this.canvas.getObjects().filter(obj => obj.name === 'mask');
        if (maskObjs.length === 0) {
            return null;
        }
        
        const tempCanvas = document.createElement('canvas');
        tempCanvas.width = this.baseImageObject.width;
        tempCanvas.height = this.baseImageObject.height;
        const ctx = tempCanvas.getContext('2d');
        
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, tempCanvas.width, tempCanvas.height);
        
        ctx.fillStyle = '#FFFFFF';
        ctx.strokeStyle = '#FFFFFF';
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        
        maskObjs.forEach(obj => {
            if (obj.path && Array.isArray(obj.path)) {
                ctx.beginPath();
                ctx.lineWidth = obj.strokeWidth || this._baseBrushSize;
                
                obj.path.forEach((cmd) => {
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
            return;
        }
        
        // Save state before drawing starts (for proper undo)
        if (this.canvas.isDrawingMode && e.button === 0) {
            this._saveStateBeforeAction();
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
        
        // Determine the name based on current tool
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
        
        // Set layer properties on the path
        path.name = pathName;
        path.layerId = layerId;
        path.layer = layerName;
        
        // Override toObject to include layer properties
        const originalToObject = path.toObject.bind(path);
        path.toObject = function(propertiesToInclude) {
            const obj = originalToObject(propertiesToInclude);
            obj.name = this.name;
            obj.layerId = this.layerId;
            obj.layer = this.layer;
            return obj;
        };
        
        // Notify layer change for masking
        if (isMaskTool) {
            this._notifyLayerChanged();
        }
        
        this._afterPathCreated();
        
        // Mark as dirty
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCanvasModified');
        }
    }
    
    _eraseDrawingAtPath(eraserPath) {
        // Remove drawing objects that intersect with eraser path
        const eraserBounds = eraserPath.getBoundingRect();
        
        // Only erase from the active layer
        const drawingObjects = this.canvas.getObjects().filter(obj => 
            obj.name === 'drawing' && obj.layerId === this.activeLayerId
        );
        const toRemove = [];
        
        drawingObjects.forEach(drawingObj => {
            const drawingBounds = drawingObj.getBoundingRect();
            
            // Check if bounding boxes intersect
            if (this._boundsIntersect(eraserBounds, drawingBounds)) {
                toRemove.push(drawingObj);
            }
        });
        
        toRemove.forEach(obj => {
            this.canvas.remove(obj);
        });
        
        this.canvas.renderAll();
        
        // Mark as dirty if we removed anything
        if (toRemove.length > 0 && this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCanvasModified');
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
        
        // Delete selected objects
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
    
    /**
     * Delete currently selected objects
     */
    _deleteSelectedObjects() {
        const activeObjects = this.canvas.getActiveObjects();
        if (activeObjects.length === 0) return;
        
        // Save state before deletion
        this._saveStateBeforeAction();
        
        activeObjects.forEach(obj => {
            // Don't delete base image or locked layer objects
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
    
    // ==================== .NET Callbacks ====================
    
    _notifyImageLoaded(width, height) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnImageLoaded', width, height);
        }
    }
    
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
    
    _notifyLayerChanged() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnLayerChanged', this.getLayerState());
        }
    }
    
    // ==================== Image Import ====================
    
    /**
     * Import an image from a data URL and add it to the active layer
     * @param {string} dataUrl - The image data URL
     * @param {object} options - Optional settings (x, y, scale, name)
     */
    async importImage(dataUrl, options = {}) {
        if (!dataUrl) {
            console.warn('No image data provided for import');
            return null;
        }
        
        try {
            let img;
            const result = fabric.Image.fromURL(dataUrl, { crossOrigin: 'anonymous' });
            
            if (result instanceof Promise) {
                img = await result;
            } else {
                img = await new Promise((resolve, reject) => {
                    fabric.Image.fromURL(dataUrl, (loadedImg) => {
                        if (loadedImg) resolve(loadedImg);
                        else reject(new Error('Failed to load imported image'));
                    }, { crossOrigin: 'anonymous' });
                });
            }
            
            if (!img) throw new Error('Failed to import image - null result');
            
            // Get canvas/base dimensions for scaling
            const maxSize = this.baseImageObject 
                ? Math.min(this.baseImageObject.width, this.baseImageObject.height) * 0.8
                : Math.min(this.canvas.getWidth(), this.canvas.getHeight()) * 0.6;
            
            // Scale down if larger than max size
            let scale = 1;
            if (img.width > maxSize || img.height > maxSize) {
                scale = maxSize / Math.max(img.width, img.height);
            }
            
            // Limit import size to 4096px
            if (img.width > this.options.maxCanvasSize || img.height > this.options.maxCanvasSize) {
                const maxDim = Math.max(img.width, img.height);
                scale = Math.min(scale, this.options.maxCanvasSize / maxDim);
            }
            
            // Calculate center position
            const centerX = this.baseImageObject 
                ? this.baseImageObject.width / 2
                : this.canvas.getWidth() / 2 / this.canvas.getZoom();
            const centerY = this.baseImageObject 
                ? this.baseImageObject.height / 2
                : this.canvas.getHeight() / 2 / this.canvas.getZoom();
            
            // Apply settings
            img.set({
                left: options.x !== undefined ? options.x : centerX,
                top: options.y !== undefined ? options.y : centerY,
                originX: 'center',
                originY: 'center',
                scaleX: options.scale !== undefined ? options.scale : scale,
                scaleY: options.scale !== undefined ? options.scale : scale,
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
            
            // Set layer properties
            const activeLayer = this.getActiveLayer();
            img.name = 'imported';
            img.layerId = activeLayer?.id || this.activeLayerId;
            img.layer = activeLayer?.name || 'Drawing Layer';
            
            // Override toObject to include layer properties
            const originalToObject = img.toObject.bind(img);
            img.toObject = function(propertiesToInclude) {
                const obj = originalToObject(propertiesToInclude);
                obj.name = this.name;
                obj.layerId = this.layerId;
                obj.layer = this.layer;
                return obj;
            };
            
            // Save state before adding
            this._saveStateBeforeAction();
            
            this.canvas.add(img);
            this.canvas.setActiveObject(img);
            this.canvas.renderAll();
            
            this._afterPathCreated();
            
            // Notify that canvas was modified
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnCanvasModified');
            }
            
            // Notify import success
            this._notifyImageImported(img.width * img.scaleX, img.height * img.scaleY);
            
            return img;
        } catch (error) {
            console.error('Error importing image:', error);
            throw error;
        }
    }
    
    /**
     * Import image from file input
     * @param {File} file - The file to import
     */
    async importImageFromFile(file) {
        if (!file || !file.type.startsWith('image/')) {
            console.warn('Invalid file for import');
            return null;
        }
        
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = async (e) => {
                try {
                    const img = await this.importImage(e.target.result);
                    resolve(img);
                } catch (err) {
                    reject(err);
                }
            };
            reader.onerror = () => reject(new Error('Failed to read file'));
            reader.readAsDataURL(file);
        });
    }
    
    /**
     * Import image from clipboard
     * @param {ClipboardEvent} e - The clipboard event
     */
    async importFromClipboard(e) {
        if (!e.clipboardData) return null;
        
        const items = e.clipboardData.items;
        for (let i = 0; i < items.length; i++) {
            if (items[i].type.startsWith('image/')) {
                const file = items[i].getAsFile();
                if (file) {
                    return await this.importImageFromFile(file);
                }
            }
        }
        
        return null;
    }
    
    /**
     * Setup paste handler for clipboard image import
     */
    _setupPasteHandler() {
        this._boundPasteHandler = async (e) => {
            // Don't intercept if typing in an input
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            
            // Check if the modal overlay is present (editor is open)
            const overlay = document.querySelector('.image-editor-overlay');
            if (!overlay) return;
            
            // Check if there's image data in clipboard
            if (!e.clipboardData || !e.clipboardData.items) return;
            
            let hasImage = false;
            for (let i = 0; i < e.clipboardData.items.length; i++) {
                if (e.clipboardData.items[i].type.startsWith('image/')) {
                    hasImage = true;
                    break;
                }
            }
            
            if (!hasImage) return;
            
            // Prevent the paste from going to other handlers (like the main I2I page)
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            
            await this.importFromClipboard(e);
        };
        
        // Use capture phase to intercept before other handlers
        document.addEventListener('paste', this._boundPasteHandler, true);
    }
    
    /**
     * Setup drag and drop for image import
     */
    _setupDragDrop() {
        const wrapper = document.getElementById(this.canvasId)?.parentElement;
        if (!wrapper) return;
        
        // Prevent default drag behaviors
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            wrapper.addEventListener(eventName, (e) => {
                e.preventDefault();
                e.stopPropagation();
            }, false);
        });
        
        // Visual feedback
        ['dragenter', 'dragover'].forEach(eventName => {
            wrapper.addEventListener(eventName, () => {
                wrapper.classList.add('drag-over');
            }, false);
        });
        
        ['dragleave', 'drop'].forEach(eventName => {
            wrapper.addEventListener(eventName, () => {
                wrapper.classList.remove('drag-over');
            }, false);
        });
        
        // Handle drop
        wrapper.addEventListener('drop', async (e) => {
            const files = e.dataTransfer?.files;
            if (files && files.length > 0) {
                for (let i = 0; i < files.length; i++) {
                    if (files[i].type.startsWith('image/')) {
                        await this.importImageFromFile(files[i]);
                        break; // Only import first image
                    }
                }
            }
        }, false);
    }
    
    /**
     * Notify .NET of image import
     */
    _notifyImageImported(width, height) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnImageImported', Math.round(width), Math.round(height));
        }
    }
    
    // ==================== Cleanup ====================
    
    dispose() {
        document.removeEventListener('keydown', this._boundKeyDown);
        document.removeEventListener('keyup', this._boundKeyUp);
        document.removeEventListener('paste', this._boundPasteHandler, true);
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
