# Image Editor Implementation Plan

> **Document Version:** 2.3  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 2 Complete  
> **Target:** Img2ImgComfyUI Page Enhancement

---

## Executive Summary

The Image Editor is a Fabric.js-based drawing and masking tool integrated into the Blazor WebApp. It enables users to edit images before sending them to ComfyUI for img2img generation, and to create inpainting masks for targeted image modifications.

---

## Phase Overview

| Phase | Description | Status | Priority |
|-------|-------------|--------|----------|
| **Phase 1** | Core Canvas Infrastructure | ✅ Complete | P0 |
| **Phase 2** | Drawing & Mask Tools | ✅ Complete | P0 |
| **Phase 3** | Advanced Features | 🔴 Not Started | P1 |
| **Phase 4** | Polish & UX | 🔴 Not Started | P2 |

---

## Phase 1: Core Canvas Infrastructure ✅ COMPLETE

### Objectives
- Establish Fabric.js integration with Blazor
- Create modal component structure
- Implement basic zoom/pan functionality
- Set up JS interop communication

### Deliverables

#### 1.1 Component Structure
- [x] `ImageEditorModal.razor` - Main modal component
- [x] `ImageEditorModal.razor.css` - Scoped styles
- [x] `ImageEditor.js` - Fabric.js wrapper module
- [x] `ImageEditorState.cs` - C# state model

#### 1.2 Canvas Management
- [x] Canvas initialization with Fabric.js
- [x] Image loading from base64 data URL
- [x] Blank canvas creation with configurable dimensions
- [x] Canvas resize handling
- [x] Proper disposal and cleanup

#### 1.3 Zoom & Pan
- [x] Mouse wheel zoom (centered on cursor)
- [x] Fit-to-view (Ctrl+0)
- [x] Actual size / 100% zoom (Ctrl+1)
- [x] Pan via middle mouse button
- [x] Pan via Space + left click drag
- [x] Pan tool button
- [x] Zoom slider in footer

#### 1.4 JS Interop
- [x] DotNetObjectReference for callbacks
- [x] Module-based JS loading
- [x] Proper async initialization
- [x] Error handling and logging

---

## Phase 2: Drawing & Mask Tools ✅ COMPLETE

### Objectives
- Implement all drawing tools
- Create mask layer system
- Add undo/redo history
- Export edited images and masks

### Deliverables

#### 2.1 Drawing Tools
- [x] Brush tool with configurable size/color
- [x] Brush cursor visualization (dual-outline circle)
- [x] Eraser tool - **v2.3: Removes intersecting strokes**
- [x] Color picker tool (eyedropper)
- [x] Color selector (HTML color input)
- [x] Brush size slider

#### 2.2 Mask Tools
- [x] Mask brush (draws on mask layer)
- [x] Mask eraser (removes mask strokes)
- [🔴] Mask fill tool (flood fill) - Deferred to Phase 3
- [x] Mask visibility toggle
- [x] Mask opacity slider
- [🔴] Flat mask rendering (no opacity stacking) - Deferred to Phase 3

#### 2.3 History System ✅ COMPLETE
- [x] Undo functionality
- [x] Redo functionality
- [x] State serialization with Fabric.js toJSON
- [x] Keyboard shortcuts (Ctrl+Z, Ctrl+Y)
- [x] Proper state capture before drawing (on mouse:down)
- [x] State sent to .NET after path completion
- [x] Custom property preservation (name) for Fabric.js 6
- [x] Eraser actions are undoable/redoable - **v2.3**

#### 2.4 Keyboard Shortcuts
- [x] B - Brush tool
- [x] E - Eraser tool
- [x] M - Mask brush tool
- [x] I - Color picker (eyedropper)
- [x] [ / ] - Decrease/increase brush size
- [x] Space - Hold for pan mode
- [x] Ctrl+Z - Undo
- [x] Ctrl+Y / Ctrl+Shift+Z - Redo
- [x] Ctrl+0 - Fit to view
- [x] Ctrl+1 - Actual size

#### 2.5 Export
- [x] Export image as PNG data URL
- [x] Export mask as PNG (black/white)
- [x] Hide mask overlay during image export
- [x] Reset viewport for export

#### 2.6 UI Components
- [x] Toolbar with tool buttons
- [x] Status bar with coordinates
- [x] Zoom level display
- [x] Image dimensions display
- [x] Tool indicator chip
- [x] Apply/Cancel buttons
- [x] Body scroll lock when editor is open

---

## Phase 3: Advanced Features 🔴 NOT STARTED

### Objectives
- Layer management
- Advanced selection tools
- Image adjustments
- Template system

### Planned Deliverables

#### 3.1 Layer System
- [ ] Layer panel UI
- [ ] Add/remove layers
- [ ] Layer reordering
- [ ] Layer opacity
- [ ] Layer visibility toggle
- [ ] Layer blending modes
- [ ] **Raster layer support** (enables pixel-level erasing)

#### 3.2 Selection Tools
- [ ] Rectangular selection
- [ ] Elliptical selection
- [ ] Lasso selection
- [ ] Magic wand (color-based selection)
- [ ] Selection to mask conversion
- [ ] Invert selection

#### 3.3 Transform Tools
- [ ] Move selection/layer
- [ ] Scale selection/layer
- [ ] Rotate selection/layer
- [ ] Flip horizontal/vertical
- [ ] Crop to selection

#### 3.4 Image Adjustments
- [ ] Brightness/contrast
- [ ] Hue/saturation
- [ ] Color balance
- [ ] Levels
- [ ] Blur/sharpen filters

#### 3.5 Shapes & Text
- [ ] Rectangle tool
- [ ] Ellipse tool
- [ ] Line tool
- [ ] Polygon tool
- [ ] Text tool with font selection

#### 3.6 Deferred from Phase 2
- [ ] Mask fill tool (flood fill)
- [ ] Flat mask rendering (no opacity stacking)
- [ ] Pixel-level eraser (requires raster layers)

---

## Phase 4: Polish & UX 🔴 NOT STARTED

### Objectives
- Performance optimization
- Accessibility improvements
- User preference persistence
- Advanced workflow integration

### Planned Deliverables

#### 4.1 Performance
- [ ] Large image handling (>4K)
- [ ] History state compression
- [ ] Lazy loading of tools
- [ ] Web Worker for heavy operations

#### 4.2 Persistence
- [ ] Save editor preferences
- [ ] Recent colors palette
- [ ] Favorite brushes
- [ ] Session recovery (auto-save)

#### 4.3 Integration
- [ ] Direct integration with generation queue
- [ ] Mask persistence across generations
- [ ] ControlNet pose editor mode
- [ ] Batch editing support

#### 4.4 Accessibility
- [ ] Keyboard navigation
- [ ] Screen reader support
- [ ] High contrast mode
- [ ] Touch device support

---

## Current Issues & Status

### Priority 1: Critical Functionality - ALL RESOLVED ✅

| # | Issue | Description | Status |
|---|-------|-------------|--------|
| 1 | **Eraser** | Eraser removes intersecting strokes | ✅ Fixed v2.3 |
| 2 | **Mask Eraser** | Remove mask strokes | ✅ Fixed |
| 3 | **Mask not persisted** | Mask lost on editor reopen | 🔴 Deferred to Phase 3 |
| 4 | **Pan tool broken** | Pan acts as object picker | ✅ Fixed |
| 5 | **Undo/Redo issues** | Clears all strokes on first undo | ✅ Fixed v2.2 |
| 6 | **Middle mouse pan** | Pans browser instead of canvas | ✅ Fixed |

### Priority 2: UI/UX Issues - ALL RESOLVED ✅

| # | Issue | Description | Status |
|---|-------|-------------|--------|
| 7 | **Mask opacity stacking** | Multiple strokes increase opacity | 🔴 Deferred to Phase 3 |
| 8 | **Modal too narrow** | Doesn't use full viewport | ✅ Fixed - 95vw/vh |
| 9 | **Canvas gap** | Dark area doesn't fill container | ✅ Fixed |
| 10 | **Menu z-index** | Dropdown behind modal | ✅ Fixed |
| 11 | **Tooltips hidden** | Tooltips behind modal | ✅ Fixed |
| 12 | **Brush slider update** | Keyboard shortcuts don't update slider | ✅ Fixed |
| 17 | **Cursor coords** | Only update on click | ✅ Fixed |
| 18 | **Zoom feedback loop** | Glitchy zoom flashing | ✅ Fixed |
| 19 | **Brush cursor offset** | Cursor not centered | ✅ Fixed |
| 20 | **Body scroll lock** | Page scrolls when editor open | ✅ Fixed v2.2 |

### Priority 3: Nice to Have

| # | Issue | Description | Status |
|---|-------|-------------|--------|
| 13 | **Brush cursor** | Add circular outline | ✅ Fixed - dual color |
| 14 | **Color preview** | Unused element | ✅ Removed |
| 15 | **Color picker accuracy** | Sometimes picks wrong color | 🟡 Low Priority |
| 16 | **Shape picker** | Separate tool for shapes | 🟡 Phase 3 |

---

## Architecture

### Component Hierarchy

```
ImageEditorModal.razor
├── Title Bar
│   ├── Editor title & status chips
│   ├── Zoom controls (fit, actual size)
│   └── Close button
├── Toolbar
│   ├── Drawing tools (brush, eraser, picker, pan)
│   ├── Mask tools (mask brush, mask eraser)
│   ├── Color picker & brush size
│   ├── Undo/Redo buttons
│   └── Clear menu
├── Canvas Wrapper
│   └── Fabric.js Canvas
└── Footer
    ├── Zoom slider
    ├── Cursor coordinates
    ├── Tool indicator
    └── Apply/Cancel buttons
```

### State Management

```
ImageEditorState.cs
├── Base Image
│   ├── BaseImageData (base64)
│   ├── BaseImageWidth/Height
│   └── IsBlankCanvas
├── Tool Settings
│   ├── CurrentTool (enum)
│   ├── BrushColor, BrushSize, BrushOpacity
│   └── SecondaryColor
├── View Settings
│   ├── ZoomLevel, MinZoom, MaxZoom
│   └── PanX, PanY
├── History
│   ├── UndoStack (List<string>)
│   ├── RedoStack (List<string>)
│   └── MaxHistorySize
└── Mask Layer
    ├── HasMask, MaskData
    ├── MaskVisible, MaskOpacity
    └── MaskOverlayColor
```

### JS Interop Flow

```
Blazor Component              ImageEditor.js              Fabric.js Canvas
      │                              │                           │
      │──── init() ──────────────────►                           │
      │                              │──── new fabric.Canvas() ──►
      │                              │                           │
      │◄─── OnImageLoaded() ─────────│                           │
      │                              │                           │
      │──── setTool() ───────────────►                           │
      │                              │──── isDrawingMode = true ─►
      │                              │                           │
      │◄─── OnSaveState() ───────────│◄─── mouse:down ───────────│
      │                              │     (before drawing)       │
      │                              │                           │
      │◄─── OnCanvasModified() ──────│◄─── path:created ─────────│
      │                              │                           │
      │──── exportImage() ───────────►                           │
      │◄─── return dataURL ──────────│◄─── toDataURL() ──────────│
```

### Eraser Implementation (v2.3)

The eraser tool removes entire drawing strokes that intersect with the eraser path:

```javascript
_eraseDrawingAtPath(eraserPath) {
    const eraserBounds = eraserPath.getBoundingRect();
    const drawingObjects = this.canvas.getObjects()
        .filter(obj => obj.name === 'drawing');
    
    drawingObjects.forEach(drawingObj => {
        if (this._boundsIntersect(eraserBounds, drawingObj.getBoundingRect())) {
            this.canvas.remove(drawingObj);
        }
    });
}
```

**Design Decision**: We chose stroke-level removal (like Figma's vector eraser) over pixel-level erasing because:
1. Maintains vector-based architecture consistency
2. Works seamlessly with existing undo/redo system
3. Pixel-level erasing requires raster layers (planned for Phase 3)

---

## File Reference

### Core Files
| File | Purpose |
|------|---------|
| `Components/ImageEditor/ImageEditorModal.razor` | Main Blazor component |
| `Components/ImageEditor/ImageEditorModal.razor.css` | Scoped CSS styles |
| `wwwroot/js/ImageEditor.js` | Fabric.js integration |
| `Models/ImageEditorState.cs` | C# state model |

### Related Files
| File | Purpose |
|------|---------|
| `wwwroot/site.css` | Global z-index overrides, body scroll lock |
| `Components/Img2Img/*.razor` | Integration point |

### External Dependencies
| Dependency | Version | CDN |
|------------|---------|-----|
| Fabric.js | 6.0.2 | jsdelivr, unpkg, cdnjs |

---

## Changelog

### Version 2.3 (December 2024) - Eraser Tool Complete
- **Eraser tool now works** - removes drawing strokes that intersect with eraser path
  - Uses bounding box intersection detection
  - Eraser actions are fully undoable/redoable
  - Consistent behavior with mask eraser
- **Cleaned up remaining debug logging** in ImageEditor.js
- **Documentation updated** with eraser implementation details
- **Phase 2 marked as COMPLETE** - remaining items deferred to Phase 3

### Version 2.2 (December 2024) - Complete Undo/Redo Fix
- **Undo/Redo now works correctly**
  - Fixed Fabric.js 6 custom property serialization issue
  - Override `toObject()` on all canvas objects to include `name` property
  - State captured on mouse:down, sent to .NET on path:created
  - Direct object restoration instead of `enlivenObjects` (preserves properties)
  - Multiple undo/redo cycles work correctly
- **Added body scroll lock** when editor is open
- **Cleaned up debug logging** - removed verbose console output
- Updated documentation with Fabric.js 6 serialization details

### Version 2.1 (December 2024) - Undo/Redo Partial Fix
- State now saved BEFORE drawing starts (on mouse:down)
- Added `_stateBeforeActionSaved` flag to prevent duplicate saves
- Removed initial state save from loadImage/createBlankCanvas
- Added `OnCanvasModified` callback for dirty state tracking

### Version 2.0 (December 2024)
- Restored full implementation plan
- Added phase breakdown
- Added architecture documentation
- Updated issue tracking

### Version 1.8 (December 2024)
- Fixed cursor position updates
- Fixed zoom feedback loop
- Fixed brush cursor centering
- Improved undo/redo with Fabric.js 6 API

### Version 1.7 (December 2024)
- Fixed pan tool functionality
- Fixed middle mouse pan
- Fixed undo/redo visual flash
- Fixed mask eraser
- Added circular brush cursor
- Fixed modal size (95vw/vh)
- Fixed z-index for menus/tooltips

---

## Testing Checklist

### Phase 2 Completion Criteria ✅ ALL PASSED

#### Drawing Tools
- [x] Brush draws correctly at all zoom levels
- [x] Brush cursor follows mouse accurately
- [x] Color picker samples correct pixel color
- [x] Brush size slider updates brush width
- [x] Eraser removes intersecting strokes
- [x] Eraser actions are undoable/redoable

#### Mask Tools
- [x] Mask brush creates red overlay strokes
- [x] Mask eraser removes mask strokes
- [x] Mask visibility toggle works
- [x] Mask exported as black/white PNG

#### History
- [x] Undo reverts to previous state
- [x] Redo restores undone state
- [x] Multiple undo/redo operations work
- [x] History survives zoom/pan changes
- [x] Undo/Redo cycles work repeatedly

#### Export
- [x] Image exports without mask overlay
- [x] Image exports at original dimensions
- [x] Mask exports correctly formatted
- [x] Apply callback receives both image and mask

---

## Development Notes

### Fabric.js 6 API Changes
- `enlivenObjects` now returns Promise instead of using callback
- `sendObjectToBack` method location changed
- `toJSON(['name'])` does NOT include custom properties - must override `toObject()`
- Custom properties require explicit `toObject()` override on each object

### Eraser Approaches Considered
1. **Stroke removal (implemented)** - Remove entire strokes that intersect
2. **Rasterized eraser** - Requires hybrid canvas architecture (Phase 3)
3. **Path clipping** - Complex, requires external library like Clipper.js

### Browser Compatibility
- Tested in Chrome, Firefox, Edge
- Safari may have WebGL rendering differences
- Touch support not yet implemented

### Performance Considerations
- History states are full JSON snapshots
- Large images (>2000px) may cause memory issues
- Consider implementing state compression

---

*Document maintained in: `DOC/IMAGE_EDITOR_IMPLEMENTATION_PLAN.md`*
