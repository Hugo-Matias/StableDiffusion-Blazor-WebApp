/**
 * ImageEditor.selection.js - Selection tools for mask creation
 * 
 * This module provides:
 * - Rectangle, Ellipse, and Lasso selection tools
 * - Selection to mask conversion (add/subtract)
 * - Marching ants visualization
 * - Selection modifiers (Shift for constrain, Alt for center-out)
 */

/**
 * Selection mixin for ImageEditor
 */
export const SelectionMixin = {
    
    /**
     * Initialize the selection system
     * Called from main ImageEditor constructor
     */
    _initSelectionSystem() {
        // Current selection object on canvas
        this._selectionObject = null;
        
        // Selection state
        this._isDrawingSelection = false;
        this._selectionStartPoint = null;
        this._selectionType = 'rect'; // 'rect', 'ellipse', 'lasso'
        this._lassoPoints = [];
        
        // Selection style
        this._selectionStyle = {
            fill: 'rgba(66, 133, 244, 0.2)',
            stroke: '#4285f4',
            strokeWidth: 1,
            strokeDashArray: [5, 5],
            strokeDashOffset: 0,
            selectable: false,
            evented: false,
            excludeFromExport: true,
            name: '_selection'
        };
        
        // Marching ants animation
        this._marchingAntsInterval = null;
    },
    
    /**
     * Set the selection tool type
     * @param {string} type - 'rect', 'ellipse', or 'lasso'
     */
    setSelectionType(type) {
        if (['rect', 'ellipse', 'lasso'].includes(type)) {
            this._selectionType = type;
            
            // Notify Blazor of selection type change
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnSelectionTypeChanged', type);
            }
        }
    },
    
    /**
     * Get the current selection type
     * @returns {string} Current selection type
     */
    getSelectionType() {
        return this._selectionType;
    },
    
    /**
     * Check if a selection tool is active
     * @returns {boolean}
     */
    isSelectionToolActive() {
        return ['selectrect', 'selectellipse', 'selectlasso'].includes(this.currentTool);
    },
    
    /**
     * Check if there's an active selection
     * @returns {boolean}
     */
    hasSelection() {
        return this._selectionObject !== null;
    },
    
    /**
     * Start a new selection
     * @param {number} x - Start X coordinate (canvas space)
     * @param {number} y - Start Y coordinate (canvas space)
     */
    startSelection(x, y) {
        // Clear any existing selection (this also resets state)
        this.clearSelection();
        
        this._isDrawingSelection = true;
        this._selectionStartPoint = { x, y };
        
        if (this._selectionType === 'lasso') {
            this._lassoPoints = [{ x, y }];
            this._createLassoSelection();
        } else {
            this._createShapeSelection(x, y, 0, 0);
        }
        
        this._startMarchingAnts();
    },
    
    /**
     * Update the selection during drag
     * @param {number} x - Current X coordinate
     * @param {number} y - Current Y coordinate
     * @param {boolean} shiftKey - Shift modifier pressed
     * @param {boolean} altKey - Alt modifier pressed
     */
    updateSelection(x, y, shiftKey = false, altKey = false) {
        if (!this._isDrawingSelection || !this._selectionStartPoint) return;
        
        if (this._selectionType === 'lasso') {
            this._lassoPoints.push({ x, y });
            this._updateLassoSelection();
        } else {
            this._updateShapeSelection(x, y, shiftKey, altKey);
        }
    },
    
    /**
     * Finish the selection
     */
    finishSelection() {
        if (!this._isDrawingSelection) return;
        
        this._isDrawingSelection = false;
        
        if (this._selectionType === 'lasso' && this._lassoPoints.length > 2) {
            // Close the lasso path
            this._closeLassoSelection();
        }
        
        // Check if selection is valid (has area)
        if (this._selectionObject) {
            const bounds = this._selectionObject.getBoundingRect();
            if (bounds.width < 5 || bounds.height < 5) {
                // Selection too small, clear it
                this.clearSelection();
                return;
            }
        }
        
        // Notify Blazor that selection changed
        this._notifySelectionStateChanged(this.hasSelection());
    },
    
    /**
     * Clear the current selection and reset all selection state
     */
    clearSelection() {
        this._stopMarchingAnts();
        
        if (this._selectionObject) {
            this.canvas.remove(this._selectionObject);
            this._selectionObject = null;
        }
        
        this._isDrawingSelection = false;
        this._selectionStartPoint = null;
        this._lassoPoints = [];
        
        this.canvas.renderAll();
        this._notifySelectionStateChanged(false);
    },
    
    /**
     * Invert the current selection and immediately apply to mask
     * This is an immediate operation - it applies the inverted area to the mask right away
     */
    invertSelection() {
        if (!this.baseImageObject) return;
        
        // Get current selection geometry
        const currentGeometry = this._selectionObject ? this._getSelectionGeometry() : null;
        
        if (!currentGeometry) {
            // No selection to invert - select entire canvas
            const imgWidth = this.baseImageObject.width;
            const imgHeight = this.baseImageObject.height;
            
            this._stopMarchingAnts();
            if (this._selectionObject) {
                this.canvas.remove(this._selectionObject);
            }
            
            this._selectionType = 'rect';
            this._selectionObject = new fabric.Rect({
                left: 0,
                top: 0,
                width: imgWidth,
                height: imgHeight,
                ...this._selectionStyle
            });
            
            this.canvas.add(this._selectionObject);
            this._startMarchingAnts();
            this.canvas.renderAll();
            this._notifySelectionStateChanged(true);
            return;
        }
        
        // We have a selection - apply inverted mask immediately
        const width = this.baseImageObject.width;
        const height = this.baseImageObject.height;
        
        // Save state BEFORE making changes (this is the state we'll undo TO)
        const stateBeforeChange = this.getState();
        
        // Ensure mask canvas exists
        if (!this._maskCanvas || this._maskCanvas.width !== width || this._maskCanvas.height !== height) {
            this._createMaskCanvas(width, height);
        }
        
        // Draw inverted mask: fill everything, then cut out the selection
        this._maskCtx.save();
        this._maskCtx.fillStyle = '#FFFFFF';
        this._maskCtx.fillRect(0, 0, width, height);
        
        // Use destination-out to cut the hole
        this._maskCtx.globalCompositeOperation = 'destination-out';
        this._maskCtx.fillStyle = '#FFFFFF';
        this._drawGeometryToContext(this._maskCtx, currentGeometry);
        this._maskCtx.restore();
        
        // Create mask objects for tracking
        this._createMaskObjectFromGeometry({ type: 'rect', left: 0, top: 0, width, height }, 'add');
        this._createMaskObjectFromGeometry(currentGeometry, 'subtract');
        
        // Update the mask overlay
        if (typeof this._updateMaskOverlay === 'function') {
            this._updateMaskOverlay();
        }
        
        // Save the state BEFORE the change to history (enables undo)
        if (typeof this._notifySaveState === 'function') {
            this._notifySaveState(stateBeforeChange);
        }
        
        if (typeof this._notifyMaskChanged === 'function') {
            this._notifyMaskChanged(true);
        }
        if (typeof this._notifyCanvasModified === 'function') {
            this._notifyCanvasModified();
        }
        
        // Clear the selection
        this.clearSelection();
    },
    
    /**
     * Convert the current selection to mask
     * @param {string} mode - 'add' or 'subtract'
     */
    selectionToMask(mode = 'add') {
        if (!this._selectionObject || !this.baseImageObject) {
            console.warn('selectionToMask: No selection or base image');
            return;
        }
        
        const geometry = this._getSelectionGeometry();
        if (!geometry) {
            console.warn('selectionToMask: Could not get geometry');
            return;
        }
        
        // Save state BEFORE making changes (this is the state we'll undo TO)
        const stateBeforeChange = this.getState();
        
        // Ensure mask canvas exists
        const width = this.baseImageObject.width;
        const height = this.baseImageObject.height;
        
        if (!this._maskCanvas || this._maskCanvas.width !== width || this._maskCanvas.height !== height) {
            this._createMaskCanvas(width, height);
        }
        
        // Draw the selection to mask
        this._maskCtx.save();
        this._maskCtx.fillStyle = mode === 'add' ? '#FFFFFF' : '#000000';
        this._drawGeometryToContext(this._maskCtx, geometry);
        this._maskCtx.restore();
        
        // Create a mask object for tracking (for undo/redo compatibility)
        this._createMaskObjectFromGeometry(geometry, mode);
        
        // Update the mask overlay
        if (typeof this._updateMaskOverlay === 'function') {
            this._updateMaskOverlay();
        }
        
        // Now save the state BEFORE the change to history (this enables undo)
        if (typeof this._notifySaveState === 'function') {
            this._notifySaveState(stateBeforeChange);
        }
        
        // Notify mask changed
        if (typeof this._notifyMaskChanged === 'function') {
            this._notifyMaskChanged(true);
        }
        if (typeof this._notifyCanvasModified === 'function') {
            this._notifyCanvasModified();
        }
        
        // Clear the selection after applying to mask
        this.clearSelection();
    },
    
    /**
     * Create a rectangle or ellipse selection
     */
    _createShapeSelection(x, y, width, height) {
        const props = {
            left: x,
            top: y,
            width: Math.abs(width) || 1,
            height: Math.abs(height) || 1,
            ...this._selectionStyle
        };
        
        if (this._selectionType === 'ellipse') {
            props.rx = Math.abs(width) / 2 || 0.5;
            props.ry = Math.abs(height) / 2 || 0.5;
            props.originX = 'center';
            props.originY = 'center';
            delete props.width;
            delete props.height;
            this._selectionObject = new fabric.Ellipse(props);
        } else {
            this._selectionObject = new fabric.Rect(props);
        }
        
        this.canvas.add(this._selectionObject);
    },
    
    /**
     * Update rectangle or ellipse selection during drag
     */
    _updateShapeSelection(x, y, shiftKey = false, altKey = false) {
        if (!this._selectionObject || !this._selectionStartPoint) return;
        
        const startX = this._selectionStartPoint.x;
        const startY = this._selectionStartPoint.y;
        
        // Calculate raw dimensions from start to current point
        let rawWidth = x - startX;
        let rawHeight = y - startY;
        
        // Apply Shift constraint (square/circle) FIRST
        if (shiftKey) {
            const maxDim = Math.max(Math.abs(rawWidth), Math.abs(rawHeight));
            rawWidth = rawWidth >= 0 ? maxDim : -maxDim;
            rawHeight = rawHeight >= 0 ? maxDim : -maxDim;
        }
        
        let left, top, width, height;
        
        // Apply Alt (center-out) mode
        if (altKey) {
            // Draw from center: start point is center, dimensions extend equally in all directions
            width = Math.abs(rawWidth) * 2;
            height = Math.abs(rawHeight) * 2;
            left = startX - Math.abs(rawWidth);
            top = startY - Math.abs(rawHeight);
        } else {
            // Normal mode: start point is corner
            if (rawWidth >= 0) {
                left = startX;
                width = rawWidth;
            } else {
                left = startX + rawWidth;
                width = -rawWidth;
            }
            
            if (rawHeight >= 0) {
                top = startY;
                height = rawHeight;
            } else {
                top = startY + rawHeight;
                height = -rawHeight;
            }
        }
        
        // Ensure minimum dimensions
        width = Math.max(width, 1);
        height = Math.max(height, 1);
        
        if (this._selectionType === 'ellipse') {
            this._selectionObject.set({
                left: left + width / 2,
                top: top + height / 2,
                rx: width / 2,
                ry: height / 2
            });
        } else {
            this._selectionObject.set({
                left: left,
                top: top,
                width: width,
                height: height
            });
        }
        
        this._selectionObject.setCoords();
        this.canvas.renderAll();
    },
    
    /**
     * Create initial lasso selection path
     */
    _createLassoSelection() {
        if (this._lassoPoints.length < 1) return;
        
        const point = this._lassoPoints[0];
        
        // Create a simple path with just the starting point
        this._selectionObject = new fabric.Path(`M ${point.x} ${point.y}`, {
            ...this._selectionStyle,
            fill: 'transparent', // No fill while drawing
            stroke: this._selectionStyle.stroke,
            strokeWidth: this._selectionStyle.strokeWidth,
            strokeDashArray: this._selectionStyle.strokeDashArray
        });
        
        this.canvas.add(this._selectionObject);
    },
    
    /**
     * Update lasso selection during drag
     */
    _updateLassoSelection() {
        if (!this._selectionObject || this._lassoPoints.length < 2) return;
        
        // Remove old path and create new one (Fabric.js path updating is tricky)
        this.canvas.remove(this._selectionObject);
        
        const pathData = this._lassoPointsToPathData(false);
        
        this._selectionObject = new fabric.Path(pathData, {
            ...this._selectionStyle,
            fill: 'transparent', // No fill while drawing
            stroke: this._selectionStyle.stroke,
            strokeWidth: this._selectionStyle.strokeWidth,
            strokeDashArray: this._selectionStyle.strokeDashArray
        });
        
        this.canvas.add(this._selectionObject);
        this.canvas.renderAll();
    },
    
    /**
     * Close the lasso path when finished
     */
    _closeLassoSelection() {
        if (!this._selectionObject || this._lassoPoints.length < 3) return;
        
        // Remove old path
        this.canvas.remove(this._selectionObject);
        
        // Create closed path with fill
        const pathData = this._lassoPointsToPathData(true);
        
        this._selectionObject = new fabric.Path(pathData, {
            ...this._selectionStyle,
            fill: this._selectionStyle.fill,
            stroke: this._selectionStyle.stroke,
            strokeWidth: this._selectionStyle.strokeWidth,
            strokeDashArray: this._selectionStyle.strokeDashArray
        });
        
        this.canvas.add(this._selectionObject);
        this.canvas.renderAll();
    },
    
    /**
     * Convert lasso points to SVG path data
     * @param {boolean} close - Whether to close the path
     */
    _lassoPointsToPathData(close = false) {
        if (this._lassoPoints.length < 1) return 'M 0 0';
        
        let path = `M ${this._lassoPoints[0].x} ${this._lassoPoints[0].y}`;
        
        for (let i = 1; i < this._lassoPoints.length; i++) {
            path += ` L ${this._lassoPoints[i].x} ${this._lassoPoints[i].y}`;
        }
        
        if (close) {
            path += ' Z';
        }
        
        return path;
    },
    
    /**
     * Get the geometry of the current selection
     * @returns {object|null} Geometry object
     */
    _getSelectionGeometry() {
        if (!this._selectionObject) return null;
        
        const obj = this._selectionObject;
        const type = this._selectionType;
        
        if (type === 'rect') {
            return {
                type: 'rect',
                left: obj.left,
                top: obj.top,
                width: obj.width * (obj.scaleX || 1),
                height: obj.height * (obj.scaleY || 1)
            };
        } else if (type === 'ellipse') {
            return {
                type: 'ellipse',
                cx: obj.left,
                cy: obj.top,
                rx: obj.rx * (obj.scaleX || 1),
                ry: obj.ry * (obj.scaleY || 1)
            };
        } else if (type === 'lasso') {
            return {
                type: 'lasso',
                points: [...this._lassoPoints]
            };
        }
        
        return null;
    },
    
    /**
     * Draw geometry to a canvas context
     * @param {CanvasRenderingContext2D} ctx - The context
     * @param {object} geometry - The geometry to draw
     */
    _drawGeometryToContext(ctx, geometry) {
        ctx.beginPath();
        
        if (geometry.type === 'rect') {
            ctx.rect(geometry.left, geometry.top, geometry.width, geometry.height);
        } else if (geometry.type === 'ellipse') {
            ctx.ellipse(
                geometry.cx, 
                geometry.cy, 
                geometry.rx, 
                geometry.ry, 
                0, 0, Math.PI * 2
            );
        } else if (geometry.type === 'lasso' && geometry.points && geometry.points.length > 2) {
            ctx.moveTo(geometry.points[0].x, geometry.points[0].y);
            for (let i = 1; i < geometry.points.length; i++) {
                ctx.lineTo(geometry.points[i].x, geometry.points[i].y);
            }
            ctx.closePath();
        }
        
        ctx.fill();
    },
    
    /**
     * Create a mask object from geometry for undo/redo tracking
     * The object is added to canvas and will be serialized with the state
     */
    _createMaskObjectFromGeometry(geometry, mode) {
        let maskShape;
        const color = mode === 'add' ? '#FFFFFF' : '#000000';
        
        if (geometry.type === 'rect') {
            maskShape = new fabric.Rect({
                left: geometry.left,
                top: geometry.top,
                width: geometry.width,
                height: geometry.height,
                fill: color,
                stroke: color,
                strokeWidth: 0,
                selectable: false,
                evented: false,
                visible: false
            });
        } else if (geometry.type === 'ellipse') {
            maskShape = new fabric.Ellipse({
                left: geometry.cx,
                top: geometry.cy,
                rx: geometry.rx,
                ry: geometry.ry,
                originX: 'center',
                originY: 'center',
                fill: color,
                stroke: color,
                strokeWidth: 0,
                selectable: false,
                evented: false,
                visible: false
            });
        } else if (geometry.type === 'lasso' && geometry.points && geometry.points.length > 2) {
            const pathData = this._lassoPointsToPathData(true);
            maskShape = new fabric.Path(pathData, {
                fill: color,
                stroke: color,
                strokeWidth: 0,
                selectable: false,
                evented: false,
                visible: false
            });
        }
        
        if (maskShape) {
            // Set name property for serialization
            maskShape.name = 'mask';
            maskShape.layerId = this.activeLayerId;
            maskShape.layer = 'Mask';
            
            // Override toObject to include custom properties in serialization
            const originalToObject = maskShape.toObject.bind(maskShape);
            maskShape.toObject = function(propertiesToInclude) {
                const result = originalToObject(propertiesToInclude);
                result.name = this.name;
                result.layerId = this.layerId;
                result.layer = this.layer;
                return result;
            };
            
            // Add to canvas and mask objects array
            this.canvas.add(maskShape);
            if (!this.maskObjects) {
                this.maskObjects = [];
            }
            this.maskObjects.push(maskShape);
        }
    },
    
    /**
     * Start marching ants animation
     */
    _startMarchingAnts() {
        this._stopMarchingAnts();
        
        if (!this._selectionObject) return;
        
        let offset = 0;
        this._marchingAntsInterval = setInterval(() => {
            if (this._selectionObject) {
                offset = (offset + 1) % 10;
                this._selectionObject.set('strokeDashOffset', offset);
                this.canvas.renderAll();
            }
        }, 50);
    },
    
    /**
     * Stop marching ants animation
     */
    _stopMarchingAnts() {
        if (this._marchingAntsInterval) {
            clearInterval(this._marchingAntsInterval);
            this._marchingAntsInterval = null;
        }
    },
    
    /**
     * Notify Blazor that selection state changed
     */
    _notifySelectionStateChanged(hasSelection) {
        // Use the callback from CallbacksMixin
        if (typeof this._notifySelectionChanged === 'function') {
            this._notifySelectionChanged(hasSelection);
        } else if (this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync('OnSelectionChanged', hasSelection);
            } catch (e) {
                console.warn('Failed to notify selection changed:', e);
            }
        }
    },
    
    /**
     * Dispose selection system resources
     */
    _disposeSelectionSystem() {
        this._stopMarchingAnts();
        if (this._selectionObject) {
            this.canvas.remove(this._selectionObject);
            this._selectionObject = null;
        }
        this._lassoPoints = [];
    }
};
