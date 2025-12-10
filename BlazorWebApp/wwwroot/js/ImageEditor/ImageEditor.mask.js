/**
 * ImageEditor.mask.js - Mask rendering with flat compositing and visual overlay
 * 
 * This module provides:
 * - Offscreen canvas for mask compositing (no opacity stacking)
 * - Multiple preview modes (overlay, binary, marching ants, blackout, whiteout)
 * - Binary B/W mask export
 */

/**
 * Mask preview modes
 */
export const MaskPreviewMode = {
    OVERLAY: 'overlay',         // Colored overlay with stripe pattern (default)
    BINARY: 'binary',           // Black/white preview (what gets sent to API)
    MARCHING_ANTS: 'marchingants', // Outline only, no fill
    BLACKOUT: 'blackout',       // Black out unmasked areas
    WHITEOUT: 'whiteout'        // White out masked areas
};

/**
 * Mask mixin for ImageEditor
 */
export const MaskMixin = {
    
    /**
     * Initialize the mask system
     * Called from main ImageEditor constructor
     */
    _initMaskSystem() {
        // Offscreen canvas for mask compositing
        this._maskCanvas = null;
        this._maskCtx = null;
        
        // Mask overlay element for display
        this._maskOverlayEl = null;
        
        // Mask display settings - use options if available
        this._maskDisplayColor = this.options?.maskColor || '#FF0000';
        this._maskDisplayOpacity = this.options?.maskOpacity || 0.5;
        
        // Preview mode
        this._maskPreviewMode = MaskPreviewMode.OVERLAY;
        
        // Cached marching ants canvas (static, no animation for performance)
        this._cachedAntsCanvas = null;
        this._antsCacheValid = false;
    },
    
    /**
     * Create the offscreen mask canvas
     * @param {number} width - Canvas width
     * @param {number} height - Canvas height
     */
    _createMaskCanvas(width, height) {
        this._maskCanvas = document.createElement('canvas');
        this._maskCanvas.width = width;
        this._maskCanvas.height = height;
        this._maskCtx = this._maskCanvas.getContext('2d');
        
        // Initialize with transparent
        this._clearMaskCanvas();
    },
    
    /**
     * Clear the mask canvas
     */
    _clearMaskCanvas() {
        if (!this._maskCtx || !this._maskCanvas) return;
        this._maskCtx.clearRect(0, 0, this._maskCanvas.width, this._maskCanvas.height);
    },
    
    /**
     * Resize the mask canvas
     * @param {number} width - New width
     * @param {number} height - New height
     */
    _resizeMaskCanvas(width, height) {
        if (!this._maskCanvas) {
            this._createMaskCanvas(width, height);
            return;
        }
        
        // Save current content
        const tempCanvas = document.createElement('canvas');
        tempCanvas.width = this._maskCanvas.width;
        tempCanvas.height = this._maskCanvas.height;
        const tempCtx = tempCanvas.getContext('2d');
        tempCtx.drawImage(this._maskCanvas, 0, 0);
        
        // Resize
        this._maskCanvas.width = width;
        this._maskCanvas.height = height;
        
        // Restore content (scaled if needed)
        this._maskCtx.drawImage(tempCanvas, 0, 0);
    },
    
    /**
     * Composite all mask strokes to the offscreen canvas
     * This creates a flat mask with no opacity stacking
     * Handles both path strokes and filled shapes (from selection tools)
     * Properly handles add (white) and subtract (erase) operations
     */
    _compositeMaskToCanvas() {
        if (!this._maskCtx || !this._maskCanvas) return;
        
        // Clear the mask canvas
        this._clearMaskCanvas();
        
        // Set up for drawing
        this._maskCtx.lineCap = 'round';
        this._maskCtx.lineJoin = 'round';
        
        // Draw all mask objects in order
        this.maskObjects.forEach(obj => {
            // Determine if this is an add or subtract operation based on fill color
            const fillColor = obj.fill || '#FFFFFF';
            const isSubtract = fillColor === '#000000' || fillColor === 'black';
            
            // Save context state
            this._maskCtx.save();
            
            if (isSubtract) {
                // Subtract mode: erase from the mask using destination-out
                this._maskCtx.globalCompositeOperation = 'destination-out';
                this._maskCtx.fillStyle = '#FFFFFF'; // Color doesn't matter for destination-out
                this._maskCtx.strokeStyle = '#FFFFFF';
            } else {
                // Add mode: draw white on the mask
                this._maskCtx.globalCompositeOperation = 'source-over';
                this._maskCtx.fillStyle = '#FFFFFF';
                this._maskCtx.strokeStyle = '#FFFFFF';
            }
            
            // Handle different object types
            if (obj.type === 'rect') {
                // Rectangle from selection
                this._maskCtx.fillRect(obj.left, obj.top, obj.width, obj.height);
            } else if (obj.type === 'ellipse') {
                // Ellipse from selection
                this._maskCtx.beginPath();
                this._maskCtx.ellipse(
                    obj.left, 
                    obj.top, 
                    obj.rx, 
                    obj.ry, 
                    0, 0, Math.PI * 2
                );
                this._maskCtx.fill();
            } else if (obj.type === 'path' && obj.path && Array.isArray(obj.path)) {
                // Path - could be brush stroke or lasso selection
                this._maskCtx.beginPath();
                
                // Check if it's a filled path (from lasso or selection) or stroke path (from brush)
                const isFilled = obj.fill && obj.fill !== 'transparent' && obj.fill !== '';
                
                obj.path.forEach((cmd) => {
                    if (cmd[0] === 'M') {
                        this._maskCtx.moveTo(cmd[1], cmd[2]);
                    } else if (cmd[0] === 'Q') {
                        this._maskCtx.quadraticCurveTo(cmd[1], cmd[2], cmd[3], cmd[4]);
                    } else if (cmd[0] === 'L') {
                        this._maskCtx.lineTo(cmd[1], cmd[2]);
                    } else if (cmd[0] === 'Z' || cmd[0] === 'z') {
                        this._maskCtx.closePath();
                    }
                });
                
                if (isFilled) {
                    // Filled path (lasso selection or filled shape)
                    this._maskCtx.fill();
                } else {
                    // Stroke path (brush)
                    this._maskCtx.lineWidth = obj.strokeWidth || this._baseBrushSize;
                    this._maskCtx.stroke();
                }
            }
            
            // Restore context state
            this._maskCtx.restore();
        });
    },
    
    /**
     * Create the mask overlay element for visual display
     */
    _createMaskOverlay() {
        const wrapper = document.getElementById(this.canvasId)?.parentElement;
        if (!wrapper) return;
        
        // Remove existing overlay if any
        this._removeMaskOverlay();
        
        // Create overlay container with the mask image
        this._maskOverlayEl = document.createElement('div');
        this._maskOverlayEl.className = 'mask-display-overlay';
        this._maskOverlayEl.style.cssText = `
            position: absolute;
            pointer-events: none;
            z-index: 10;
            overflow: hidden;
        `;
        
        // Create the mask image element (colored mask with stripe pattern baked in)
        const maskImg = document.createElement('img');
        maskImg.className = 'mask-display-image';
        maskImg.style.cssText = `
            width: 100%;
            height: 100%;
            object-fit: fill;
            pointer-events: none;
        `;
        this._maskOverlayEl.appendChild(maskImg);
        
        wrapper.appendChild(this._maskOverlayEl);
        
        // Initially hide
        this._maskOverlayEl.style.display = 'none';
    },
    
    /**
     * Remove the mask overlay elements
     */
    _removeMaskOverlay() {
        if (this._maskOverlayEl && this._maskOverlayEl.parentElement) {
            this._maskOverlayEl.parentElement.removeChild(this._maskOverlayEl);
        }
        this._maskOverlayEl = null;
    },
    
    /**
     * Update the mask overlay display
     * Called after each mask stroke or when visibility/opacity changes
     */
    _updateMaskOverlay() {
        if (!this._maskOverlayEl) {
            this._createMaskOverlay();
        }
        
        if (!this._maskOverlayEl) return;
        
        // Check if we have mask content
        const hasMask = this.maskObjects && this.maskObjects.length > 0;
        
        if (!hasMask || !this.maskVisible) {
            this._maskOverlayEl.style.display = 'none';
            return;
        }
        
        // Ensure mask canvas exists and matches base image size
        if (this.baseImageObject) {
            const width = this.baseImageObject.width;
            const height = this.baseImageObject.height;
            
            if (!this._maskCanvas || this._maskCanvas.width !== width || this._maskCanvas.height !== height) {
                this._createMaskCanvas(width, height);
            }
        }
        
        // Composite the mask (invalidates ants cache)
        this._compositeMaskToCanvas();
        this._antsCacheValid = false;
        
        // Create the display canvas based on preview mode
        const displayCanvas = this._createPreviewCanvas();
        if (!displayCanvas) return;
        
        // Update the mask image
        const maskImg = this._maskOverlayEl.querySelector('.mask-display-image');
        if (maskImg) {
            maskImg.src = displayCanvas.toDataURL('image/png');
        }
        
        // Show the overlay and sync position
        this._maskOverlayEl.style.display = 'block';
        this._syncMaskOverlayWithCanvas();
    },
    
    /**
     * Create a preview canvas based on the current preview mode
     * @returns {HTMLCanvasElement} Canvas with styled mask
     */
    _createPreviewCanvas() {
        switch (this._maskPreviewMode) {
            case MaskPreviewMode.BINARY:
                return this._createBinaryPreviewCanvas();
            case MaskPreviewMode.MARCHING_ANTS:
                return this._createMarchingAntsPreviewCanvas();
            case MaskPreviewMode.BLACKOUT:
                return this._createBlackoutPreviewCanvas();
            case MaskPreviewMode.WHITEOUT:
                return this._createWhiteoutPreviewCanvas();
            case MaskPreviewMode.OVERLAY:
            default:
                return this._createStyledMaskCanvas();
        }
    },
    
    /**
     * Create a styled version of the mask for display (OVERLAY mode)
     * Includes color, stripe pattern, and opacity - all baked into one image
     * @returns {HTMLCanvasElement} Canvas with styled mask
     */
    _createStyledMaskCanvas() {
        if (!this._maskCanvas) return null;
        
        const width = this._maskCanvas.width;
        const height = this._maskCanvas.height;
        
        const styledCanvas = document.createElement('canvas');
        styledCanvas.width = width;
        styledCanvas.height = height;
        const ctx = styledCanvas.getContext('2d');
        
        // Fill with mask color
        ctx.fillStyle = this._maskDisplayColor;
        ctx.globalAlpha = this._maskDisplayOpacity;
        ctx.fillRect(0, 0, width, height);
        
        // Add subtle diagonal stripe pattern for visual distinction
        ctx.globalAlpha = this._maskDisplayOpacity * 0.2;
        ctx.strokeStyle = '#FFFFFF';
        ctx.lineWidth = 1;
        
        const stripeSpacing = 16;
        for (let i = -height; i < width + height; i += stripeSpacing) {
            ctx.beginPath();
            ctx.moveTo(i, height);
            ctx.lineTo(i + height, 0);
            ctx.stroke();
        }
        
        // Reset alpha
        ctx.globalAlpha = 1.0;
        
        // Use the B/W mask as alpha (destination-in compositing)
        ctx.globalCompositeOperation = 'destination-in';
        ctx.drawImage(this._maskCanvas, 0, 0);
        
        return styledCanvas;
    },
    
    /**
     * Create binary B/W preview canvas (BINARY mode)
     * Shows exactly what will be sent to the API
     * @returns {HTMLCanvasElement} Canvas with B/W mask
     */
    _createBinaryPreviewCanvas() {
        if (!this._maskCanvas) return null;
        
        const width = this._maskCanvas.width;
        const height = this._maskCanvas.height;
        
        const binaryCanvas = document.createElement('canvas');
        binaryCanvas.width = width;
        binaryCanvas.height = height;
        const ctx = binaryCanvas.getContext('2d');
        
        // Fill with black background
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, width, height);
        
        // Draw the mask in white
        ctx.drawImage(this._maskCanvas, 0, 0);
        
        return binaryCanvas;
    },
    
    /**
     * Create marching ants outline preview (MARCHING_ANTS mode)
     * Shows only the outline of the mask with static dashed lines
     * Uses caching for performance - only rebuilds when mask changes
     * @returns {HTMLCanvasElement} Canvas with outline
     */
    _createMarchingAntsPreviewCanvas() {
        if (!this._maskCanvas) return null;
        
        // Return cached version if valid
        if (this._antsCacheValid && this._cachedAntsCanvas) {
            return this._cachedAntsCanvas;
        }
        
        const width = this._maskCanvas.width;
        const height = this._maskCanvas.height;
        
        const antsCanvas = document.createElement('canvas');
        antsCanvas.width = width;
        antsCanvas.height = height;
        const ctx = antsCanvas.getContext('2d');
        
        // Get image data to find edges
        const maskData = this._maskCtx.getImageData(0, 0, width, height);
        const data = maskData.data;
        
        // Use typed array for faster access
        const alphaChannel = new Uint8Array(width * height);
        for (let i = 0; i < width * height; i++) {
            alphaChannel[i] = data[i * 4 + 3] > 128 ? 1 : 0;
        }
        
        // Helper to check if pixel is inside mask
        const isInMask = (x, y) => {
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            return alphaChannel[y * width + x] === 1;
        };
        
        // Draw edges directly without building chains
        // This is faster and works well for static display
        ctx.strokeStyle = '#000000';
        ctx.lineWidth = 1;
        ctx.setLineDash([4, 4]);
        ctx.lineDashOffset = 0;
        
        ctx.beginPath();
        
        // Scan for horizontal edges (between rows)
        for (let y = 0; y <= height; y++) {
            let inEdge = false;
            let edgeStart = 0;
            
            for (let x = 0; x < width; x++) {
                const current = isInMask(x, y);
                const above = isInMask(x, y - 1);
                const isEdge = current !== above;
                
                if (isEdge && !inEdge) {
                    // Start of edge segment
                    edgeStart = x;
                    inEdge = true;
                } else if (!isEdge && inEdge) {
                    // End of edge segment - draw it
                    ctx.moveTo(edgeStart, y);
                    ctx.lineTo(x, y);
                    inEdge = false;
                }
            }
            // Close any remaining edge
            if (inEdge) {
                ctx.moveTo(edgeStart, y);
                ctx.lineTo(width, y);
            }
        }
        
        // Scan for vertical edges (between columns)
        for (let x = 0; x <= width; x++) {
            let inEdge = false;
            let edgeStart = 0;
            
            for (let y = 0; y < height; y++) {
                const current = isInMask(x, y);
                const left = isInMask(x - 1, y);
                const isEdge = current !== left;
                
                if (isEdge && !inEdge) {
                    // Start of edge segment
                    edgeStart = y;
                    inEdge = true;
                } else if (!isEdge && inEdge) {
                    // End of edge segment - draw it
                    ctx.moveTo(x, edgeStart);
                    ctx.lineTo(x, y);
                    inEdge = false;
                }
            }
            // Close any remaining edge
            if (inEdge) {
                ctx.moveTo(x, edgeStart);
                ctx.lineTo(x, height);
            }
        }
        
        ctx.stroke();
        
        // Draw white outline offset for contrast
        ctx.strokeStyle = '#FFFFFF';
        ctx.lineDashOffset = 4;
        ctx.stroke();
        
        // Cache the result
        this._cachedAntsCanvas = antsCanvas;
        this._antsCacheValid = true;
        
        return antsCanvas;
    },
    
    /**
     * Create blackout preview (BLACKOUT mode)
     * Shows only the masked areas, blacks out everything else
     * @returns {HTMLCanvasElement} Canvas with blackout effect
     */
    _createBlackoutPreviewCanvas() {
        if (!this._maskCanvas || !this.baseImageObject) return null;
        
        const width = this._maskCanvas.width;
        const height = this._maskCanvas.height;
        
        const blackoutCanvas = document.createElement('canvas');
        blackoutCanvas.width = width;
        blackoutCanvas.height = height;
        const ctx = blackoutCanvas.getContext('2d');
        
        // Fill with semi-transparent black
        ctx.fillStyle = 'rgba(0, 0, 0, 0.85)';
        ctx.fillRect(0, 0, width, height);
        
        // Cut out the masked areas (show what will be inpainted)
        ctx.globalCompositeOperation = 'destination-out';
        ctx.drawImage(this._maskCanvas, 0, 0);
        
        return blackoutCanvas;
    },
    
    /**
     * Create whiteout preview (WHITEOUT mode)
     * Shows white over masked areas to highlight what will be changed
     * @returns {HTMLCanvasElement} Canvas with whiteout effect
     */
    _createWhiteoutPreviewCanvas() {
        if (!this._maskCanvas) return null;
        
        const width = this._maskCanvas.width;
        const height = this._maskCanvas.height;
        
        const whiteoutCanvas = document.createElement('canvas');
        whiteoutCanvas.width = width;
        whiteoutCanvas.height = height;
        const ctx = whiteoutCanvas.getContext('2d');
        
        // Fill with semi-transparent white
        ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
        ctx.fillRect(0, 0, width, height);
        
        // Use mask as alpha
        ctx.globalCompositeOperation = 'destination-in';
        ctx.drawImage(this._maskCanvas, 0, 0);
        
        return whiteoutCanvas;
    },
    
    /**
     * Start marching ants animation for preview
     */
    _startPreviewMarchingAnts() {
        // No-op - marching ants are now cached as a static canvas
    },
    
    /**
     * Stop marching ants animation for preview
     */
    _stopPreviewMarchingAnts() {
        // No-op - marching ants are now cached as a static canvas
    },
    
    /**
     * Sync the mask overlay position with the Fabric.js canvas viewport
     */
    _syncMaskOverlayWithCanvas() {
        if (!this._maskOverlayEl || !this.baseImageObject) return;
        
        const vpt = this.canvas.viewportTransform;
        const zoom = vpt[0]; // Scale factor
        const panX = vpt[4]; // X translation
        const panY = vpt[5]; // Y translation
        
        // Calculate the position and size of the base image in screen coordinates
        const imgWidth = this.baseImageObject.width * zoom;
        const imgHeight = this.baseImageObject.height * zoom;
        
        // The base image is at (0,0) in canvas coordinates
        // After viewport transform, it appears at (panX, panY)
        const imgLeft = panX;
        const imgTop = panY;
        
        // Update overlay element position
        this._maskOverlayEl.style.left = `${imgLeft}px`;
        this._maskOverlayEl.style.top = `${imgTop}px`;
        this._maskOverlayEl.style.width = `${imgWidth}px`;
        this._maskOverlayEl.style.height = `${imgHeight}px`;
        this._maskOverlayEl.style.transform = 'none';
    },
    
    /**
     * Set mask preview mode
     * @param {string} mode - One of MaskPreviewMode values
     */
    setMaskPreviewMode(mode) {
        const validModes = Object.values(MaskPreviewMode);
        if (!validModes.includes(mode)) {
            console.warn(`Invalid mask preview mode: ${mode}`);
            return;
        }
        
        this._maskPreviewMode = mode;
        
        // Refresh the overlay
        if (this.maskVisible && this.maskObjects && this.maskObjects.length > 0) {
            this._updateMaskOverlay();
        }
        
        // Notify Blazor
        if (this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync('OnMaskPreviewModeChanged', mode);
            } catch (e) {
                // Ignore if method doesn't exist
            }
        }
    },
    
    /**
     * Get current mask preview mode
     * @returns {string} Current preview mode
     */
    getMaskPreviewMode() {
        return this._maskPreviewMode;
    },
    
    /**
     * Cycle to next preview mode
     * @returns {string} New preview mode
     */
    cycleMaskPreviewMode() {
        const modes = Object.values(MaskPreviewMode);
        const currentIndex = modes.indexOf(this._maskPreviewMode);
        const nextIndex = (currentIndex + 1) % modes.length;
        const nextMode = modes[nextIndex];
        
        this.setMaskPreviewMode(nextMode);
        return nextMode;
    },
    
    /**
     * Set mask display color
     * @param {string} color - Hex color for mask display
     */
    setMaskDisplayColor(color) {
        this._maskDisplayColor = color;
        // Also update the mask brush color for drawing
        this.maskColor = color;
        
        // Update the brush if currently using mask tool
        if (this.currentTool === 'maskbrush') {
            this._setupBrush('maskbrush');
        }
        
        // Re-render the overlay with new color
        if (this.maskVisible && this.maskObjects && this.maskObjects.length > 0) {
            this._updateMaskOverlay();
        }
    },
    
    /**
     * Set mask display opacity
     * @param {number} opacity - Opacity value (0-1)
     */
    setMaskDisplayOpacity(opacity) {
        this._maskDisplayOpacity = opacity;
        // Re-render the overlay with new opacity
        if (this.maskVisible && this.maskObjects && this.maskObjects.length > 0) {
            this._updateMaskOverlay();
        }
    },
    
    /**
     * Set mask visibility
     * @param {boolean} visible - Whether mask is visible
     */
    setMaskVisibility(visible) {
        this.maskVisible = visible;
        
        if (visible && this.maskObjects && this.maskObjects.length > 0) {
            // Force refresh the overlay when becoming visible
            this._updateMaskOverlay();
        } else if (this._maskOverlayEl) {
            this._maskOverlayEl.style.display = 'none';
            this._stopPreviewMarchingAnts();
        }
        
        // Also update the individual mask stroke visibility (for legacy compatibility)
        this.maskObjects.forEach(obj => {
            obj.set('visible', false); // Always hidden - we use overlay
        });
        this.canvas.renderAll();
    },
    
    /**
     * Export the mask as a binary black and white image
     * @returns {string|null} Data URL of the B/W mask PNG
     */
    exportMaskBinary() {
        if (!this.baseImageObject) {
            return null;
        }
        
        if (!this.maskObjects || this.maskObjects.length === 0) {
            return null;
        }
        
        // Ensure mask canvas is up to date
        const width = this.baseImageObject.width;
        const height = this.baseImageObject.height;
        
        if (!this._maskCanvas || this._maskCanvas.width !== width || this._maskCanvas.height !== height) {
            this._createMaskCanvas(width, height);
        }
        
        // Composite all mask strokes
        this._compositeMaskToCanvas();
        
        // Create the final B/W export canvas
        const exportCanvas = document.createElement('canvas');
        exportCanvas.width = width;
        exportCanvas.height = height;
        const exportCtx = exportCanvas.getContext('2d');
        
        // Fill with black background
        exportCtx.fillStyle = '#000000';
        exportCtx.fillRect(0, 0, width, height);
        
        // Draw the mask in white
        exportCtx.drawImage(this._maskCanvas, 0, 0);
        
        return exportCanvas.toDataURL('image/png');
    },
    
    /**
     * Clear all mask data
     * Alias: clearMask (for backward compatibility with Blazor calls)
     */
    clearMaskData() {
        // Clear mask objects from canvas
        this.maskObjects.forEach(obj => {
            this.canvas.remove(obj);
        });
        this.maskObjects = [];
        
        // Clear the offscreen canvas
        this._clearMaskCanvas();
        
        // Hide overlay
        if (this._maskOverlayEl) {
            this._maskOverlayEl.style.display = 'none';
        }
        this._stopPreviewMarchingAnts();
        
        // Notify
        this._notifyMaskChanged(false);
        this.canvas.renderAll();
    },
    
    /**
     * Clear mask (alias for clearMaskData)
     */
    clearMask() {
        this.clearMaskData();
    },
    
    /**
     * Called after a mask stroke is added
     * Triggers mask overlay update
     */
    _onMaskStrokeAdded() {
        // Hide individual mask strokes from display (we use the overlay instead)
        this.maskObjects.forEach(obj => {
            obj.set('visible', false);
        });
        this.canvas.renderAll();
        
        // Update the flat overlay
        this._updateMaskOverlay();
    },
    
    /**
     * Called after a mask stroke is erased
     * Triggers mask overlay update
     */
    _onMaskStrokeErased() {
        this._updateMaskOverlay();
    },
    
    /**
     * Refresh the mask overlay (e.g., after state restore)
     * Call this when mask objects are restored from saved state
     * Also handles clearing the overlay when no mask objects exist
     */
    refreshMaskOverlay() {
        // If no mask objects, hide the overlay and clear everything
        if (!this.maskObjects || this.maskObjects.length === 0) {
            // Clear the offscreen canvas
            this._clearMaskCanvas();
            
            // Hide the overlay and clear its image source
            if (this._maskOverlayEl) {
                this._maskOverlayEl.style.display = 'none';
                
                // Also clear the image source to prevent stale display
                const maskImg = this._maskOverlayEl.querySelector('.mask-display-image');
                if (maskImg) {
                    maskImg.src = '';
                }
            }
            this._stopPreviewMarchingAnts();
            return;
        }
        
        // We have mask objects - ensure they are hidden (we use overlay)
        this.maskObjects.forEach(obj => {
            obj.set('visible', false);
        });
        this.canvas.renderAll();
        
        // Update the overlay if mask is visible
        if (this.maskVisible) {
            this._updateMaskOverlay();
        }
    },
    
    /**
     * Dispose mask system resources
     */
    _disposeMaskSystem() {
        this._removeMaskOverlay();
        this._maskCanvas = null;
        this._maskCtx = null;
        this._cachedAntsCanvas = null;
        this._antsCacheValid = false;
    }
};
