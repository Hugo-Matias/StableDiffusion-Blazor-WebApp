/**
 * ImageEditor.js - Main entry point
 * Fabric.js-based image editor module with layer support
 * 
 * This module provides:
 * - Canvas manipulation with zoom/pan
 * - Drawing tools (brush, eraser, mask)
 * - Selection tools (rect, ellipse, lasso) for mask creation
 * - Layer management
 * - History management (undo/redo)
 * - Image import/export
 * - Flat mask compositing with animated overlay
 */

import { ensureFabricLoaded } from './ImageEditor.utils.js?v=20260506-crop-tool-v5';
import { LayerMixin } from './ImageEditor.layers.js?v=20260506-crop-tool-v5';
import { CanvasMixin } from './ImageEditor.canvas.js?v=20260506-crop-tool-v5';
import { ToolsMixin } from './ImageEditor.tools.js?v=20260506-crop-tool-v5';
import { HistoryMixin } from './ImageEditor.history.js?v=20260506-crop-tool-v5';
import { EventsMixin } from './ImageEditor.events.js?v=20260506-crop-tool-v5';
import { ExportMixin } from './ImageEditor.export.js?v=20260506-crop-tool-v5';
import { CallbacksMixin } from './ImageEditor.callbacks.js?v=20260506-crop-tool-v5';
import { MaskMixin } from './ImageEditor.mask.js?v=20260506-crop-tool-v5';
import { SelectionMixin } from './ImageEditor.selection.js?v=20260506-crop-tool-v5';
import { CropMixin } from './ImageEditor.crop.js?v=20260506-crop-tool-v5';

/**
 * Initialize the ImageEditor
 * @param {string} canvasId - The ID of the canvas element
 * @param {object} dotNetRef - .NET reference for callbacks
 * @param {object} options - Configuration options
 * @returns {ImageEditor} The editor instance
 */
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

/**
 * ImageEditor class - Main editor implementation
 */
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
        
        // Clipboard for copy/paste
        this._clipboard = [];
        
        // Brush cursor
        this._brushCursorOuter = null;
        this._brushCursorInner = null;
        this._cursorVisible = false;
        
        // Bound event handlers for cleanup
        this._boundKeyDown = this._handleKeyDown.bind(this);
        this._boundKeyUp = this._handleKeyUp.bind(this);
        this._boundResize = this._handleResize.bind(this);
        this._boundPreventMiddleScroll = this._preventMiddleScroll.bind(this);
        
        // Initialize
        this._initCanvas();
        this._initMaskSystem(); // Initialize mask system
        this._initSelectionSystem(); // Initialize selection system
        this._initCropSystem(); // Initialize crop system
        this._setupEventListeners();
        this._createBrushCursor();
        this._setupDragDrop();
        this._setupPasteHandler();
        this._initializeDefaultLayers();
        
        this.isInitialized = true;
    }
    
    /**
     * Dispose and cleanup
     */
    dispose() {
        // Cleanup selection system
        if (typeof this._disposeSelectionSystem === 'function') {
            this._disposeSelectionSystem();
        }

        // Cleanup crop system
        if (typeof this._disposeCropSystem === 'function') {
            this._disposeCropSystem();
        }
        
        // Cleanup mask system
        if (typeof this._disposeMaskSystem === 'function') {
            this._disposeMaskSystem();
        }
        
        // Use the cleanup method from EventsMixin if available
        if (typeof this._cleanupEventListeners === 'function') {
            this._cleanupEventListeners();
        } else {
            // Fallback to direct cleanup
            document.removeEventListener('keydown', this._boundKeyDown);
            document.removeEventListener('keyup', this._boundKeyUp);
            window.removeEventListener('resize', this._boundResize);
        }
        
        if (this._boundPasteHandler) {
            document.removeEventListener('paste', this._boundPasteHandler, true);
        }
        
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

// Apply all mixins to the ImageEditor prototype
Object.assign(ImageEditor.prototype, LayerMixin);
Object.assign(ImageEditor.prototype, CanvasMixin);
Object.assign(ImageEditor.prototype, ToolsMixin);
Object.assign(ImageEditor.prototype, HistoryMixin);
Object.assign(ImageEditor.prototype, EventsMixin);
Object.assign(ImageEditor.prototype, ExportMixin);
Object.assign(ImageEditor.prototype, CallbacksMixin);
Object.assign(ImageEditor.prototype, MaskMixin);
Object.assign(ImageEditor.prototype, SelectionMixin);
Object.assign(ImageEditor.prototype, CropMixin);

export default ImageEditor;
