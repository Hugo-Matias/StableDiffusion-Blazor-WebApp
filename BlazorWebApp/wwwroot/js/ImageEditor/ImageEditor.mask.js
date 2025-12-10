/**
 * ImageEditor.mask.js - Mask rendering with flat compositing and visual overlay
 * 
 * This module provides:
 * - Offscreen canvas for mask compositing (no opacity stacking)
 * - Static stripe pattern overlay for visual feedback (performance-friendly)
 * - Binary B/W mask export
 */

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
     */
    _compositeMaskToCanvas() {
        if (!this._maskCtx || !this._maskCanvas) return;
        
        // Clear the mask canvas
        this._clearMaskCanvas();
        
        // Set up for drawing white strokes
        this._maskCtx.fillStyle = '#FFFFFF';
        this._maskCtx.strokeStyle = '#FFFFFF';
        this._maskCtx.lineCap = 'round';
        this._maskCtx.lineJoin = 'round';
        
        // Draw all mask objects as solid white
        this.maskObjects.forEach(obj => {
            if (!obj.path || !Array.isArray(obj.path)) return;
            
            this._maskCtx.beginPath();
            this._maskCtx.lineWidth = obj.strokeWidth || this._baseBrushSize;
            
            obj.path.forEach((cmd) => {
                if (cmd[0] === 'M') {
                    this._maskCtx.moveTo(cmd[1], cmd[2]);
                } else if (cmd[0] === 'Q') {
                    this._maskCtx.quadraticCurveTo(cmd[1], cmd[2], cmd[3], cmd[4]);
                } else if (cmd[0] === 'L') {
                    this._maskCtx.lineTo(cmd[1], cmd[2]);
                }
            });
            
            this._maskCtx.stroke();
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
        
        // Composite the mask
        this._compositeMaskToCanvas();
        
        // Create the display canvas with color, pattern, and opacity baked in
        const displayCanvas = this._createStyledMaskCanvas();
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
     * Create a styled version of the mask for display
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
        
        // Notify
        this._notifyMaskChanged(false);
        this.canvas.renderAll();
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
     */
    refreshMaskOverlay() {
        if (this.maskObjects && this.maskObjects.length > 0) {
            // Ensure all mask objects are hidden (we use overlay)
            this.maskObjects.forEach(obj => {
                obj.set('visible', false);
            });
            this.canvas.renderAll();
            
            // Update the overlay if mask is visible
            if (this.maskVisible) {
                this._updateMaskOverlay();
            }
        }
    },
    
    /**
     * Dispose mask system resources
     */
    _disposeMaskSystem() {
        this._removeMaskOverlay();
        this._maskCanvas = null;
        this._maskCtx = null;
    }
};
