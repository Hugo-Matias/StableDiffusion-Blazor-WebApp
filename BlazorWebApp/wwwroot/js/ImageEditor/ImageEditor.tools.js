/**
 * ImageEditor.tools.js - Tool handling (brush, eraser, mask, etc.)
 */

import { colorWithOpacity } from './ImageEditor.utils.js';

/**
 * Tools mixin for ImageEditor
 */
export const ToolsMixin = {
    
    /**
     * Setup the brush for a specific tool type
     */
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
    },
    
    /**
     * Create the brush cursor visualization
     */
    _createBrushCursor() {
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
    },
    
    /**
     * Update brush cursor position and size
     */
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
        
        // Bring cursor to front - use the helper method from LayerMixin
        if (typeof this._bringObjectToFront === 'function') {
            this._bringObjectToFront(this._brushCursorOuter);
            this._bringObjectToFront(this._brushCursorInner);
        } else {
            // Fallback for Fabric.js 6.x
            if (typeof this._brushCursorOuter.bringToFront === 'function') {
                this._brushCursorOuter.bringToFront();
                this._brushCursorInner.bringToFront();
            }
        }
        
        this.canvas.renderAll();
    },
    
    /**
     * Hide the brush cursor
     */
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
    },
    
    /**
     * Update brush size based on zoom
     */
    _updateBrushSize() {
        if (this.canvas.freeDrawingBrush && this._baseBrushSize) {
            const zoom = this.canvas.getZoom();
            this.canvas.freeDrawingBrush.width = this._baseBrushSize / zoom;
        }
    },
    
    /**
     * Set the current tool
     */
    setTool(tool) {
        this.previousTool = this.currentTool;
        this.currentTool = tool;
        
        // Disable object selection for most tools
        this.canvas.selection = (tool === 'select');
        
        // Update selectability of objects based on tool
        this.canvas.forEachObject(obj => {
            if (obj.name === 'baseImage' || obj.name === '_brushCursor') {
                obj.selectable = false;
                obj.evented = false;
            } else if (obj.name === 'mask') {
                obj.selectable = false;
                obj.evented = false;
            } else if (tool === 'select') {
                const layer = this.layers.find(l => l.id === obj.layerId);
                const isLocked = layer ? layer.locked : false;
                obj.selectable = !isLocked;
                obj.evented = !isLocked;
                obj.hasControls = !isLocked;
                obj.hasBorders = !isLocked;
            } else {
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
                this.canvas.discardActiveObject();
                this.canvas.renderAll();
                break;
                
            default:
                this.canvas.isDrawingMode = true;
                this._setupBrush('brush');
        }
    },
    
    /**
     * Set brush properties
     */
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
            this.canvas.freeDrawingBrush.color = colorWithOpacity(color, props.opacity);
        }
    },
    
    /**
     * Set mask settings
     */
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
    },
    
    /**
     * Get color at a specific point
     */
    getColorAtPoint(x, y) {
        const ctx = this.canvas.getContext();
        const pixel = ctx.getImageData(x, y, 1, 1).data;
        return this._rgbToHex(pixel[0], pixel[1], pixel[2]);
    },
    
    /**
     * Convert RGB to hex
     */
    _rgbToHex(r, g, b) {
        return '#' + [r, g, b].map(x => {
            const hex = x.toString(16);
            return hex.length === 1 ? '0' + hex : hex;
        }).join('');
    }
};
