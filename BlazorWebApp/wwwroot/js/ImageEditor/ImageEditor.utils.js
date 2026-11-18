/**
 * ImageEditor.utils.js - Utility functions and constants
 */

// Layer constants
export const LAYER_BASE = 'base-image-layer';
export const LAYER_MASK = 'mask-layer';

// Fabric.js CDN sources
export const FABRIC_CDNS = [
    'https://cdn.jsdelivr.net/npm/fabric@6.0.2/dist/index.min.js',
    'https://unpkg.com/fabric@6.0.2/dist/index.min.js',
    'https://cdnjs.cloudflare.com/ajax/libs/fabric.js/6.0.2/fabric.min.js'
];

let fabricLoaded = typeof fabric !== 'undefined';
let fabricLoadPromise = null;

/**
 * Dynamically load a script
 */
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

/**
 * Ensure Fabric.js is loaded
 */
export async function ensureFabricLoaded() {
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

/**
 * Generate a unique ID for layers
 */
export function generateId() {
    return 'layer-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
}

/**
 * Convert hex color to rgba with opacity
 */
export function colorWithOpacity(hexColor, opacity) {
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

/**
 * Convert RGB values to hex color
 */
export function rgbToHex(r, g, b) {
    return '#' + [r, g, b].map(x => {
        const hex = x.toString(16);
        return hex.length === 1 ? '0' + hex : hex;
    }).join('');
}

/**
 * Check if two bounding rectangles intersect
 */
export function boundsIntersect(a, b) {
    return !(a.left > b.left + b.width ||
             a.left + a.width < b.left ||
             a.top > b.top + b.height ||
             a.top + a.height < b.top);
}
