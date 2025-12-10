/**
 * ImageEditor.export.js - Export, import, and image handling functionality
 */

/**
 * Export/Import mixin for ImageEditor
 */
export const ExportMixin = {
    
    /**
     * Export the canvas as an image (without mask)
     */
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
    },
    
    /**
     * Export the mask as a black and white image
     * Uses the new flat compositing system for proper binary output
     */
    exportMask() {
        // Use the new binary export if available
        if (typeof this.exportMaskBinary === 'function') {
            return this.exportMaskBinary();
        }
        
        // Fallback to legacy export
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
    },
    
    /**
     * Import an image and add it to the active layer
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
            
            // Calculate scaling
            const maxSize = this.baseImageObject 
                ? Math.min(this.baseImageObject.width, this.baseImageObject.height) * 0.8
                : Math.min(this.canvas.getWidth(), this.canvas.getHeight()) * 0.6;
            
            let scale = 1;
            if (img.width > maxSize || img.height > maxSize) {
                scale = maxSize / Math.max(img.width, img.height);
            }
            
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
            
            const originalToObject = img.toObject.bind(img);
            img.toObject = function(propertiesToInclude) {
                const obj = originalToObject(propertiesToInclude);
                obj.name = this.name;
                obj.layerId = this.layerId;
                obj.layer = this.layer;
                return obj;
            };
            
            this._saveStateBeforeAction();
            
            this.canvas.add(img);
            this.canvas.setActiveObject(img);
            this.canvas.renderAll();
            
            this._afterPathCreated();
            
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnCanvasModified');
            }
            
            this._notifyImageImported(img.width * img.scaleX, img.height * img.scaleY);
            
            return img;
        } catch (error) {
            console.error('Error importing image:', error);
            throw error;
        }
    },
    
    /**
     * Import image from file
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
    },
    
    /**
     * Import from clipboard
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
    },
    
    /**
     * Setup paste handler
     */
    _setupPasteHandler() {
        this._boundPasteHandler = async (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            
            const overlay = document.querySelector('.image-editor-overlay');
            if (!overlay) return;
            
            if (!e.clipboardData || !e.clipboardData.items) return;
            
            let hasImage = false;
            for (let i = 0; i < e.clipboardData.items.length; i++) {
                if (e.clipboardData.items[i].type.startsWith('image/')) {
                    hasImage = true;
                    break;
                }
            }
            
            if (!hasImage) return;
            
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();
            
            await this.importFromClipboard(e);
        };
        
        document.addEventListener('paste', this._boundPasteHandler, true);
    },
    
    /**
     * Setup drag and drop
     */
    _setupDragDrop() {
        const wrapper = document.getElementById(this.canvasId)?.parentElement;
        if (!wrapper) return;
        
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            wrapper.addEventListener(eventName, (e) => {
                e.preventDefault();
                e.stopPropagation();
            }, false);
        });
        
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
        
        wrapper.addEventListener('drop', async (e) => {
            const files = e.dataTransfer?.files;
            if (files && files.length > 0) {
                for (let i = 0; i < files.length; i++) {
                    if (files[i].type.startsWith('image/')) {
                        await this.importImageFromFile(files[i]);
                        break;
                    }
                }
            }
        }, false);
    }
};
