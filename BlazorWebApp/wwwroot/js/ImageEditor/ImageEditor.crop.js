/**
 * ImageEditor.crop.js - Rectangular crop tool
 */

/**
 * Crop mixin for ImageEditor
 */
export const CropMixin = {

    /**
     * Initialize crop state and Fabric object listeners.
     */
    _initCropSystem() {
        this._cropRect = null;
        this._cropOverlayRects = [];
        this._isDrawingCrop = false;
        this._isEditingCrop = false;
        this._cropEditMode = null;
        this._cropEditHandle = null;
        this._cropEditStartPoint = null;
        this._cropEditStartBounds = null;
        this._cropStartPoint = null;
        this._minCropSize = 8;

        this._boundCropMoving = (opt) => this._handleCropMoving(opt);
        this._boundCropScaling = (opt) => this._handleCropScaling(opt);
        this._boundCropModified = (opt) => this._handleCropModified(opt);

        this.canvas.on('object:moving', this._boundCropMoving);
        this.canvas.on('object:scaling', this._boundCropScaling);
        this.canvas.on('object:modified', this._boundCropModified);
    },

    /**
     * Check whether a valid crop region exists.
     */
    hasCropRegion() {
        return this._cropRect !== null;
    },

    /**
     * Begin drawing a crop region in document coordinates.
     */
    startCrop(x, y) {
        if (!this.baseImageObject) return;

        this.currentTool = 'crop';
        this.canvas.isDrawingMode = false;
        this._hideBrushCursor();

        this.clearCrop(false);

        const point = this._clampPointToImage(x, y);
        this._isDrawingCrop = true;
        this._cropStartPoint = point;

        this._cropRect = new fabric.Rect({
            left: point.x,
            top: point.y,
            width: 1,
            height: 1,
            fill: 'rgba(255, 255, 255, 0.08)',
            stroke: '#ffffff',
            strokeWidth: 1,
            strokeDashArray: [6, 4],
            cornerStyle: 'circle',
            cornerColor: '#ffffff',
            cornerStrokeColor: '#111111',
            borderColor: '#ffffff',
            transparentCorners: false,
            hasRotatingPoint: false,
            lockRotation: true,
            lockScalingFlip: true,
            selectable: true,
            evented: false,
            hasControls: true,
            hasBorders: true,
            excludeFromExport: true,
            name: '_cropRegion'
        });

        this._configureCropControls();

        this.canvas.add(this._cropRect);
        this.canvas.setActiveObject(this._cropRect);
        this._updateCropOverlay();
        this.canvas.renderAll();
    },

    /**
     * Update the crop region during initial drag.
     */
    updateCrop(x, y) {
        if (!this._isDrawingCrop || !this._cropStartPoint || !this._cropRect) return;

        const point = this._clampPointToImage(x, y);
        const left = Math.min(this._cropStartPoint.x, point.x);
        const top = Math.min(this._cropStartPoint.y, point.y);
        const width = Math.abs(point.x - this._cropStartPoint.x);
        const height = Math.abs(point.y - this._cropStartPoint.y);

        this._cropRect.set({
            left,
            top,
            width: Math.max(width, 1),
            height: Math.max(height, 1),
            scaleX: 1,
            scaleY: 1
        });
        this._cropRect.setCoords();
        this._updateCropOverlay();
        this.canvas.renderAll();
    },

    /**
     * Finish drawing the crop region.
     */
    finishCrop() {
        if (!this._isDrawingCrop) return;

        this._isDrawingCrop = false;
        this._cropStartPoint = null;

        if (!this._cropRect) return;

        this._normalizeCropRect();
        const bounds = this._getCropBounds();

        if (!bounds || bounds.width < this._minCropSize || bounds.height < this._minCropSize) {
            this.clearCrop();
            return;
        }

        this._cropRect.set({
            selectable: true,
            evented: false,
            hasControls: true,
            hasBorders: true
        });
        this._configureCropControls();
        this.canvas.setActiveObject(this._cropRect);
        this._updateCropOverlay();
        this._notifyCropRegionChanged(true);
        this.canvas.renderAll();
    },

    /**
     * Begin moving or resizing an existing crop region.
     */
    beginCropEdit(x, y) {
        const bounds = this._getCropBounds();
        if (!bounds || !this._cropRect) return false;

        const handle = this._getCropHandleAtPoint(x, y);
        const isInside = this._isPointInCropBounds(x, y, bounds);

        if (!handle && !isInside) {
            return false;
        }

        this.currentTool = 'crop';
        this.canvas.isDrawingMode = false;
        this._hideBrushCursor();

        this._isEditingCrop = true;
        this._cropEditMode = handle ? 'resize' : 'move';
        this._cropEditHandle = handle;
        this._cropEditStartPoint = this._clampPointToImage(x, y);
        this._cropEditStartBounds = { ...bounds };

        this.canvas.setActiveObject(this._cropRect);
        this._setCropCursorForPoint(x, y);
        return true;
    },

    /**
     * Update an active crop move/resize interaction.
     */
    updateCropEdit(x, y) {
        if (!this._isEditingCrop || !this._cropEditStartPoint || !this._cropEditStartBounds) return;

        const point = this._clampPointToImage(x, y);
        const start = this._cropEditStartPoint;
        const startBounds = this._cropEditStartBounds;
        const deltaX = point.x - start.x;
        const deltaY = point.y - start.y;
        let nextBounds;

        if (this._cropEditMode === 'move') {
            const imageBounds = this._getImageBounds();
            if (!imageBounds) return;

            const maxLeft = imageBounds.right - startBounds.width;
            const maxTop = imageBounds.bottom - startBounds.height;
            nextBounds = {
                left: Math.min(Math.max(startBounds.left + deltaX, imageBounds.left), maxLeft),
                top: Math.min(Math.max(startBounds.top + deltaY, imageBounds.top), maxTop),
                width: startBounds.width,
                height: startBounds.height
            };
        } else {
            nextBounds = this._resizeCropBounds(startBounds, this._cropEditHandle, point);
        }

        this._setCropBounds(nextBounds);
        this._updateCropOverlay();
        this.canvas.renderAll();
    },

    /**
     * Finish moving or resizing an existing crop region.
     */
    finishCropEdit() {
        if (!this._isEditingCrop) return;

        this._isEditingCrop = false;
        this._cropEditMode = null;
        this._cropEditHandle = null;
        this._cropEditStartPoint = null;
        this._cropEditStartBounds = null;

        this._normalizeCropRect();
        this._updateCropOverlay();
        this._notifyCropRegionChanged(this.hasCropRegion());
        this.canvas.renderAll();
    },

    /**
     * Clear the current crop region.
     */
    clearCrop(notify = true) {
        this._removeCropOverlay();

        if (this._cropRect) {
            this.canvas.remove(this._cropRect);
            this._cropRect = null;
        }

        this._isDrawingCrop = false;
        this._isEditingCrop = false;
        this._cropEditMode = null;
        this._cropEditHandle = null;
        this._cropEditStartPoint = null;
        this._cropEditStartBounds = null;
        this._cropStartPoint = null;
        this.canvas.discardActiveObject();
        this.canvas.renderAll();

        if (notify) {
            this._notifyCropRegionChanged(false);
        }
    },

    /**
     * Apply the crop and return the flattened image payload.
     */
    applyCrop() {
        const bounds = this._getCropBounds();
        if (!bounds || bounds.width < this._minCropSize || bounds.height < this._minCropSize) {
            return null;
        }

        const hiddenDecorations = this._setEditorDecorationsVisible(false);
        const maskWasVisible = this.maskVisible;

        if (maskWasVisible && this.maskObjects) {
            this.maskObjects.forEach(obj => obj.set('visible', false));
        }

        const currentVPT = [...this.canvas.viewportTransform];
        this.canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
        this.canvas.renderAll();

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

            if (maskWasVisible && this.maskObjects) {
                this.maskObjects.forEach(obj => obj.set('visible', true));
            }

            this._restoreEditorDecorationsVisible(hiddenDecorations);
            this.clearCrop();

            return {
                image: dataUrl,
                width: bounds.width,
                height: bounds.height
            };
        } catch (error) {
            this.canvas.setViewportTransform(currentVPT);

            if (maskWasVisible && this.maskObjects) {
                this.maskObjects.forEach(obj => obj.set('visible', true));
            }

            this._restoreEditorDecorationsVisible(hiddenDecorations);
            throw error;
        }
    },

    /**
     * Temporarily hide crop/selection editor-only objects before export.
     */
    _setEditorDecorationsVisible(visible) {
        const objects = [];

        if (this._selectionObject) {
            objects.push(this._selectionObject);
        }

        if (this._cropRect) {
            objects.push(this._cropRect);
        }

        this._cropOverlayRects.forEach(obj => objects.push(obj));

        const previous = objects.map(obj => ({ obj, visible: obj.visible !== false }));
        objects.forEach(obj => obj.set('visible', visible));
        this.canvas.renderAll();

        return previous;
    },

    /**
     * Restore editor-only object visibility after export.
     */
    _restoreEditorDecorationsVisible(previous) {
        if (!Array.isArray(previous)) return;

        previous.forEach(item => {
            if (item.obj) {
                item.obj.set('visible', item.visible);
            }
        });

        this.canvas.renderAll();
    },

    _handleCropMoving(opt) {
        if (!opt || opt.target !== this._cropRect) return;

        const cropBounds = this._getCropBounds();
        const imageBounds = this._getImageBounds();
        if (!cropBounds || !imageBounds) return;

        const maxLeft = Math.max(imageBounds.left, imageBounds.right - cropBounds.width);
        const maxTop = Math.max(imageBounds.top, imageBounds.bottom - cropBounds.height);
        const left = Math.min(Math.max(this._cropRect.left || 0, imageBounds.left), maxLeft);
        const top = Math.min(Math.max(this._cropRect.top || 0, imageBounds.top), maxTop);

        this._cropRect.set({ left, top });
        this._cropRect.setCoords();
        this._updateCropOverlay();
    },

    _handleCropScaling(opt) {
        if (!opt || opt.target !== this._cropRect) return;
        this._cropRect.setCoords();
        this._updateCropOverlay();
    },

    _handleCropModified(opt) {
        if (!opt || opt.target !== this._cropRect) return;
        this._normalizeCropRect();
        this._updateCropOverlay();
        this._notifyCropRegionChanged(this.hasCropRegion());
    },

    _configureCropControls() {
        if (!this._cropRect) return;

        if (typeof this._cropRect.setControlsVisibility === 'function') {
            this._cropRect.setControlsVisibility({ mtr: false });
        }
    },

    _setCropCursorForPoint(x, y) {
        const handle = this._getCropHandleAtPoint(x, y);
        let cursor = 'crosshair';

        if (this._isEditingCrop && this._cropEditMode === 'move') {
            cursor = 'grabbing';
        } else if (handle) {
            cursor = this._getCropCursorForHandle(handle);
        } else if (this._cropRect && this._isPointInCropBounds(x, y, this._getCropBounds())) {
            cursor = 'move';
        }

        this.canvas.defaultCursor = cursor;
        if (this.canvas.upperCanvasEl) {
            this.canvas.upperCanvasEl.style.cursor = cursor;
        }
    },

    _getCropCursorForHandle(handle) {
        switch (handle) {
            case 'nw':
            case 'se':
                return 'nwse-resize';
            case 'ne':
            case 'sw':
                return 'nesw-resize';
            case 'n':
            case 's':
                return 'ns-resize';
            case 'e':
            case 'w':
                return 'ew-resize';
            default:
                return 'crosshair';
        }
    },

    _getCropHitTolerance() {
        const zoom = this.canvas?.getZoom?.() || 1;
        return Math.max(8 / zoom, 4);
    },

    _getCropHandleAtPoint(x, y) {
        const bounds = this._getCropBounds();
        if (!bounds) return null;

        const tolerance = this._getCropHitTolerance();
        const right = bounds.left + bounds.width;
        const bottom = bounds.top + bounds.height;
        const centerX = bounds.left + bounds.width / 2;
        const centerY = bounds.top + bounds.height / 2;

        const near = (value, target) => Math.abs(value - target) <= tolerance;
        const between = (value, min, max) => value >= min - tolerance && value <= max + tolerance;

        const candidates = [
            { handle: 'nw', x: bounds.left, y: bounds.top },
            { handle: 'ne', x: right, y: bounds.top },
            { handle: 'se', x: right, y: bottom },
            { handle: 'sw', x: bounds.left, y: bottom },
            { handle: 'n', x: centerX, y: bounds.top },
            { handle: 'e', x: right, y: centerY },
            { handle: 's', x: centerX, y: bottom },
            { handle: 'w', x: bounds.left, y: centerY }
        ];

        for (const candidate of candidates) {
            if (near(x, candidate.x) && near(y, candidate.y)) {
                return candidate.handle;
            }
        }

        if (near(y, bounds.top) && between(x, bounds.left, right)) return 'n';
        if (near(x, right) && between(y, bounds.top, bottom)) return 'e';
        if (near(y, bottom) && between(x, bounds.left, right)) return 's';
        if (near(x, bounds.left) && between(y, bounds.top, bottom)) return 'w';

        return null;
    },

    _isPointInCropBounds(x, y, bounds) {
        if (!bounds) return false;

        return x >= bounds.left &&
            x <= bounds.left + bounds.width &&
            y >= bounds.top &&
            y <= bounds.top + bounds.height;
    },

    _resizeCropBounds(startBounds, handle, point) {
        const imageBounds = this._getImageBounds();
        if (!imageBounds || !handle) return startBounds;

        let left = startBounds.left;
        let top = startBounds.top;
        let right = startBounds.left + startBounds.width;
        let bottom = startBounds.top + startBounds.height;

        if (handle.includes('w')) left = point.x;
        if (handle.includes('e')) right = point.x;
        if (handle.includes('n')) top = point.y;
        if (handle.includes('s')) bottom = point.y;

        left = Math.min(Math.max(left, imageBounds.left), imageBounds.right);
        right = Math.min(Math.max(right, imageBounds.left), imageBounds.right);
        top = Math.min(Math.max(top, imageBounds.top), imageBounds.bottom);
        bottom = Math.min(Math.max(bottom, imageBounds.top), imageBounds.bottom);

        if (right - left < this._minCropSize) {
            if (handle.includes('w')) {
                left = right - this._minCropSize;
            } else {
                right = left + this._minCropSize;
            }
        }

        if (bottom - top < this._minCropSize) {
            if (handle.includes('n')) {
                top = bottom - this._minCropSize;
            } else {
                bottom = top + this._minCropSize;
            }
        }

        left = Math.min(Math.max(left, imageBounds.left), imageBounds.right - this._minCropSize);
        right = Math.max(Math.min(right, imageBounds.right), imageBounds.left + this._minCropSize);
        top = Math.min(Math.max(top, imageBounds.top), imageBounds.bottom - this._minCropSize);
        bottom = Math.max(Math.min(bottom, imageBounds.bottom), imageBounds.top + this._minCropSize);

        return {
            left,
            top,
            width: right - left,
            height: bottom - top
        };
    },

    _setCropBounds(bounds) {
        if (!bounds || !this._cropRect) return;

        this._cropRect.set({
            left: bounds.left,
            top: bounds.top,
            width: Math.max(bounds.width, 1),
            height: Math.max(bounds.height, 1),
            scaleX: 1,
            scaleY: 1
        });
        this._cropRect.setCoords();
        this.canvas.setActiveObject(this._cropRect);
    },

    _getImageBounds() {
        if (!this.baseImageObject) return null;

        return {
            left: 0,
            top: 0,
            width: this.baseImageObject.width,
            height: this.baseImageObject.height,
            right: this.baseImageObject.width,
            bottom: this.baseImageObject.height
        };
    },

    _clampPointToImage(x, y) {
        const bounds = this._getImageBounds();
        if (!bounds) return { x, y };

        return {
            x: Math.min(Math.max(x, bounds.left), bounds.right),
            y: Math.min(Math.max(y, bounds.top), bounds.bottom)
        };
    },

    _getCropBounds() {
        if (!this._cropRect || !this.baseImageObject) return null;

        const rawLeft = this._cropRect.left || 0;
        const rawTop = this._cropRect.top || 0;
        const rawWidth = Math.abs((this._cropRect.width || 0) * (this._cropRect.scaleX || 1));
        const rawHeight = Math.abs((this._cropRect.height || 0) * (this._cropRect.scaleY || 1));

        const imageBounds = this._getImageBounds();
        const left = Math.max(imageBounds.left, Math.min(rawLeft, imageBounds.right));
        const top = Math.max(imageBounds.top, Math.min(rawTop, imageBounds.bottom));
        const right = Math.max(left, Math.min(rawLeft + rawWidth, imageBounds.right));
        const bottom = Math.max(top, Math.min(rawTop + rawHeight, imageBounds.bottom));

        return {
            left: Math.round(left),
            top: Math.round(top),
            width: Math.round(right - left),
            height: Math.round(bottom - top)
        };
    },

    _normalizeCropRect() {
        const bounds = this._getCropBounds();
        if (!bounds || !this._cropRect) return;

        this._cropRect.set({
            left: bounds.left,
            top: bounds.top,
            width: Math.max(bounds.width, 1),
            height: Math.max(bounds.height, 1),
            scaleX: 1,
            scaleY: 1
        });
        this._cropRect.setCoords();
    },

    _clampCropRect() {
        if (!this._cropRect) return;

        const bounds = this._getCropBounds();
        if (!bounds) return;

        this._cropRect.set({
            left: bounds.left,
            top: bounds.top,
            width: Math.max(bounds.width, 1),
            height: Math.max(bounds.height, 1),
            scaleX: 1,
            scaleY: 1
        });
        this._cropRect.setCoords();
    },

    _updateCropOverlay() {
        const imageBounds = this._getImageBounds();
        const cropBounds = this._getCropBounds();
        if (!imageBounds || !cropBounds || !this._cropRect) return;

        if (this._cropOverlayRects.length !== 4) {
            this._removeCropOverlay();

            for (let i = 0; i < 4; i++) {
                const overlay = new fabric.Rect({
                    fill: 'rgba(0, 0, 0, 0.45)',
                    selectable: false,
                    evented: false,
                    excludeFromExport: true,
                    name: '_cropOverlay'
                });
                this._cropOverlayRects.push(overlay);
                this.canvas.add(overlay);
            }
        }

        const right = cropBounds.left + cropBounds.width;
        const bottom = cropBounds.top + cropBounds.height;

        const overlayBounds = [
            { left: 0, top: 0, width: imageBounds.width, height: cropBounds.top },
            { left: 0, top: cropBounds.top, width: cropBounds.left, height: cropBounds.height },
            { left: right, top: cropBounds.top, width: imageBounds.width - right, height: cropBounds.height },
            { left: 0, top: bottom, width: imageBounds.width, height: imageBounds.height - bottom }
        ];

        this._cropOverlayRects.forEach((rect, index) => {
            const bounds = overlayBounds[index];
            rect.set({
                left: bounds.left,
                top: bounds.top,
                width: Math.max(bounds.width, 0),
                height: Math.max(bounds.height, 0),
                visible: bounds.width > 0 && bounds.height > 0
            });
            rect.setCoords();
            this._bringObjectToFront(rect);
        });

        this._bringObjectToFront(this._cropRect);
    },

    _removeCropOverlay() {
        this._cropOverlayRects.forEach(rect => this.canvas.remove(rect));
        this._cropOverlayRects = [];
    },

    _notifyCropRegionChanged(hasCropRegion) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnCropRegionChanged', hasCropRegion);
        }
    },

    _disposeCropSystem() {
        if (this.canvas) {
            this.canvas.off('object:moving', this._boundCropMoving);
            this.canvas.off('object:scaling', this._boundCropScaling);
            this.canvas.off('object:modified', this._boundCropModified);
        }

        this.clearCrop(false);
    }
};