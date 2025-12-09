# Image Editor Implementation Plan

> **Document Version:** 3.6  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 3.3 Complete  
> **Target:** Img2ImgComfyUI Page Enhancement

---

## Executive Summary

The Image Editor is a Fabric.js-based drawing and masking tool integrated into the Blazor WebApp. It enables users to edit images before sending them to ComfyUI for img2img generation, and to create inpainting masks for targeted image modifications.

**Phase 3** introduces a layer system for compositing, allowing users to import external images, organize content into layers, and build complex compositions for AI image generation.

---

## Phase Overview

| Phase | Description | Status | Priority |
|-------|-------------|--------|----------|
| **Phase 1** | Core Canvas Infrastructure | ✅ Complete | P0 |
| **Phase 2** | Drawing & Mask Tools | ✅ Complete | P0 |
| **Phase 3** | Layer System & Compositing | 🟡 In Progress | P1 |
| **Phase 4** | Polish & UX | 🔴 Not Started | P2 |

### Phase 3 Sub-phases

| Sub-phase | Description | Status |
|-----------|-------------|--------|
| **3.1** | Core Layer Infrastructure | ✅ Complete |
| **3.2** | Image Import | ✅ Complete |
| **3.3** | Layer Panel UI | ✅ Complete |
| **3.4** | Object Management | 🟡 Partial |
| **3.5** | State Preservation | ✅ Complete |
| **3.6** | Integration & Limits | ✅ Complete |

---

## Phase 1 & 2: COMPLETE

See previous documentation for Phase 1 and 2 details.

---

## Phase 3: Layer System & Compositing

### Phase 3.1: Core Layer Infrastructure ✅ COMPLETE

#### Implemented Features

| Item | Description | Status |
|------|-------------|--------|
| `LayerInfo` class | Layer data model with Id, Name, Visibility, Lock, Opacity, Order, Type | ✅ |
| `LayerType` enum | Base, Drawing, Import, Mask layer types | ✅ |
| Layer property on objects | All Fabric objects have `layer`, `layerId`, `name` properties | ✅ |
| Active layer tracking | `activeLayerId` in JS and C# state | ✅ |
| Layer assignment | New drawings assigned to active layer | ✅ |
| Default layers | "Base Image" (locked) and "Drawing Layer" created on init | ✅ |
| `InitializeDefaultLayers()` | Reset creates proper default layers | ✅ |
| Layer state in undo/redo | Layer properties preserved in serialization | ✅ |
| Select tool (V) | Tool for selecting/moving objects on canvas | ✅ |
| Layer-aware erasing | Eraser only affects objects on active layer | ✅ |

#### Data Model (ImageEditorState.cs)

```csharp
public class LayerInfo
{
    public const string BaseLayerId = "base-image-layer";
    public const string MaskLayerId = "mask-layer";
    
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }
    public float Opacity { get; set; }
    public int Order { get; set; }
    public LayerType LayerType { get; set; }
    
    public bool CanDelete => LayerType != LayerType.Base && LayerType != LayerType.Mask;
    public bool CanReorder => LayerType != LayerType.Base && LayerType != LayerType.Mask;
}

public enum LayerType { Base, Drawing, Import, Mask }
```

#### JavaScript Layer Methods (ImageEditor.js)

```javascript
// Layer management
_initializeDefaultLayers()
getActiveLayer()
setActiveLayer(layerId)
addLayer(name, type)
removeLayer(layerId)
updateLayer(layerId, props)
getLayers()
getLayerState()
restoreLayerState(state)

// Layer-aware operations
_updateLayerVisibility(layerId, visible)
_updateLayerOpacity(layerId, opacity)
_updateLayerLocked(layerId, locked)
_reorderCanvasObjects()
```

---

### Phase 3.5: State Preservation ✅ COMPLETE

#### Implemented Features

| Item | Description | Status |
|------|-------------|--------|
| Save button | Exports flattened image, preserves original base + layers | ✅ |
| Apply button | Flattens to new base, resets layer state | ✅ |
| Separate base preservation | `BaseImageData` stored separately from output | ✅ |
| Canvas JSON storage | Full canvas state saved in `CanvasJson` for re-editing | ✅ |
| State restoration | Layers and strokes restored when reopening editor | ✅ |
| Tool state sync | Current tool synced with JS on editor reopen | ✅ |
| Layer state in JSON | Layer metadata included in canvas state serialization | ✅ |
| Locked state persistence | Locked layers remain locked after restore | ✅ |

#### Save vs Apply Workflow

| Action | Output Image | Base Image (State) | Canvas JSON | Layers |
|--------|-------------|-------------------|-------------|--------|
| **Save** | Flattened export | Preserved (original) | Saved | Preserved |
| **Apply** | Flattened export | Updated (flattened) | Cleared | Reset |

**Save**: User can reopen and continue editing with original base + all strokes editable.

**Apply**: Commits all edits permanently. Flattened image becomes new base for fresh start.

---

### Phase 3.2: Image Import ✅ COMPLETE

#### Implemented Features

| Item | Description | Status |
|------|-------------|--------|
| Drag-drop import | Drop image file onto canvas | ✅ |
| File picker button | Toolbar button opens file dialog | ✅ |
| Clipboard paste | Ctrl+V / paste pastes image from clipboard | ✅ |
| Transform controls | Fabric built-in: move, resize, rotate | ✅ |
| Layer assignment | Imported images assigned to active layer | ✅ |
| Auto-scaling | Large images scaled to fit canvas (max 80%) | ✅ |
| Size limit | Images limited to 4096px max dimension | ✅ |
| Visual feedback | Drag-over state shows drop zone | ✅ |
| Image persistence | Imported images restored on editor reopening | ✅ |
| Exclusive clipboard | Paste only targets editor when open | ✅ |

#### Implementation Details

**JavaScript Methods (ImageEditor.js):**
```javascript
importImage(dataUrl, options)      // Import from data URL
importImageFromFile(file)           // Import from File object
importFromClipboard(e)              // Import from ClipboardEvent
_setupDragDrop()                    // Setup drag-drop handlers
_setupPasteHandler()                // Setup Ctrl+V paste handler (capture phase)
_restoreObjectsDirectly()           // Restore paths AND images from state
```

**Import Behavior:**
- Imported images centered on canvas
- Auto-scaled to 80% of canvas size if too large
- Assigned to active layer
- Immediately selectable with Select tool (V)
- Transform handles (resize, rotate) available
- Persisted in canvas state for restoration

---

### Phase 3.3: Layer Panel UI ✅ COMPLETE

#### Implemented Features

| Item | Description | Status |
|------|-------------|--------|
| LayerPanel component | Collapsible right sidebar panel | ✅ |
| Layer list | Display layers with type icons (brush, image, mask) | ✅ |
| Visibility toggle | Eye icon to show/hide layer | ✅ |
| Lock toggle | Lock icon to prevent edits (objects become non-selectable) | ✅ |
| Layer rename | Double-click layer name to edit | ✅ |
| Layer reorder | Up/down buttons for reorderable layers | ✅ |
| Opacity slider | Per-layer opacity control (shown when layer is active) | ✅ |
| Add layer button | Create new drawing layers | ✅ |
| Delete layer button | Remove deletable layers (not base/mask) | ✅ |
| Active layer highlight | Visual indicator for selected layer | ✅ |
| Collapsed state | Toggle button to collapse panel | ✅ |
| Layer lock enforcement | Locked layers prevent object selection/movement | ✅ |

#### Component Structure

**LayerPanel.razor:**
- Collapsible sidebar positioned in canvas wrapper
- Parameters for layers list, active layer, max count
- Event callbacks for all layer operations
- Displays layers in order (highest to lowest)
- Icons based on layer type
- Local opacity state to prevent slider feedback loops

**LayerPanel.razor.css:**
- Scoped styles for panel layout
- Active/hover states for layer items
- Opacity control styling
- Scrollable layer list

#### Integration with ImageEditorModal

```csharp
// Event handlers added to ImageEditorModal
HandleLayerSelected(string layerId)
HandleAddLayer()
HandleDeleteLayer(string layerId)
HandleLayerVisibilityChanged((string layerId, bool visible))
HandleLayerLockChanged((string layerId, bool locked))
HandleLayerOpacityChanged((string layerId, float opacity))
HandleLayerRename((string layerId, string name))
HandleLayerReorder((string layerId, int direction))
```

---

### Phase 3.4: Object Management 🟡 PARTIAL

#### Implemented

| Item | Status |
|------|--------|
| Select tool (V) | ✅ |
| Delete selected (Delete/Backspace) | ✅ |
| Object selection in Select mode | ✅ |
| Respect layer lock on delete | ✅ |
| Respect layer lock on selection | ✅ |

#### Remaining

| Item | Status |
|------|--------|
| Copy (Ctrl+C) | 🔴 |
| Paste object (Ctrl+V with objects) | 🔴 |
| Duplicate (Ctrl+D) | 🔴 |
| Flip horizontal/vertical | 🔴 |

---

### Phase 3.6: Integration & Limits ✅ COMPLETE

#### Implemented

| Item | Status |
|------|--------|
| Layer limit constant (20) | ✅ |
| Layer limit in addLayer() | ✅ |
| Import size limit (4096px) | ✅ |
| Exclusive clipboard handling | ✅ |
| Auto-reset on input image change | ✅ |

---

## Changelog

### Version 3.6 (December 2024) - Layer Locking & State Fixes

#### Layer Lock Enforcement
- **Object selectability**: Locked layers now prevent object selection and movement
- **New `_updateLayerLocked()` method**: Updates all objects in a layer when lock state changes
  - Sets `selectable`, `evented`, `lockMovementX/Y`, `lockRotation`, `lockScalingX/Y`, `hasControls`, `hasBorders`
  - Deselects any selected objects when locking the active layer
- **Select tool respects lock**: `setTool('select')` now checks layer lock status for each object
- **State restoration**: `_finalizeStateRestore()` applies locked state after restoring canvas

#### State Persistence Improvements  
- **Layer state in JSON**: `getState()` now includes full `layerState` (layers array + activeLayerId)
- **Layer restoration**: `loadState()` restores layer metadata before canvas objects
- **Object property sync**: After restoration, visibility, opacity, and locked state applied to all objects

#### Layer Panel Fixes
- **Opacity slider feedback loop**: Fixed by using local state (`_localOpacity`) that syncs only on active layer change
- **Active layer sync**: `OnParametersSet()` detects active layer changes and updates local opacity
- **Removed debug code**: Cleaned up console.log statements and debug UI elements

### Version 3.5 (December 2024) - Bug Fixes

#### Layer Panel Fixes
- **Rename Enter key**: Fixed input field not responding to Enter key - added `@onkeydown:stopPropagation`
- **Active layer highlight**: Added primary color for active layer name and icon
- **Opacity slider**: Fixed visibility with `::deep` CSS selector
- **Reorder buttons**: Moved to header for better accessibility (always visible)
- **Panel width**: Reduced to 220px for more canvas space

#### Canvas Centering
- **Layer panel offset**: Canvas now centers accounting for layer panel width (110px offset)
- Updated `fitToView()` and `zoomToActual()` in JavaScript

#### Layer Z-Order
- **Stroke ordering**: Added `_reorderCanvasObjects()` call after path creation
- Strokes now respect layer order instead of just canvas add order

#### Middle Mouse Pan
- **Event handling**: Added `auxclick` handler and upper canvas event listener
- Middle mouse button now properly initiates pan without scrolling

### Version 3.4 (December 2024) - Phase 3.3 Complete

#### Layer Panel UI
- **New Component**: `LayerPanel.razor` - Collapsible sidebar for layer management
- **Layer List**: Displays all layers with type-specific icons (brush, image, mask)
- **Visibility Toggle**: Eye icon to show/hide individual layers
- **Lock Toggle**: Lock icon to prevent accidental edits
- **Layer Rename**: Double-click layer name to edit inline
- **Layer Reorder**: Up/down buttons to change layer stacking order
- **Opacity Control**: Per-layer opacity slider (shown for active layer)
- **Add/Delete**: Create new layers, delete non-essential layers
- **Collapsible**: Toggle button to minimize panel and maximize canvas space

#### Layout Changes
- Moved active layer indicator from title bar to footer
- Canvas area restructured to accommodate layer panel
- New CSS classes: `image-editor-canvas-area`, `image-editor-canvas-main`

#### New Files
- `Components/ImageEditor/LayerPanel.razor` - Layer panel component
- `Components/ImageEditor/LayerPanel.razor.css` - Scoped styles

### Version 3.3 (December 2024) - Bug Fixes & Polish

#### Image Import
- **Drag & Drop**: Drop image files directly onto canvas
- **File Picker**: Import button in toolbar opens file dialog (MudFileUpload)
- **Clipboard Paste**: Ctrl+V or paste event imports images from clipboard
- **Auto-scaling**: Large images automatically scaled to 80% of canvas size
- **Size limits**: Maximum 4096px dimension, 10MB file size
- **Visual feedback**: Drag-over state shows "Drop image here" overlay
- **Layer assignment**: Imported images assigned to active layer
- **Transform controls**: Fabric.js built-in move, resize, rotate handles

#### New JavaScript Methods
- `importImage(dataUrl, options)` - Core import function
- `importImageFromFile(file)` - Import from File object  
- `importFromClipboard(e)` - Import from ClipboardEvent
- `_setupDragDrop()` - Initialize drag-drop handlers
- `_setupPasteHandler()` - Initialize paste event handler

#### New Blazor Integration
- `HandleFileImport(IBrowserFile)` - File upload handler
- `OnImageImported` JSInvokable callback

#### UI Updates
- Save/Apply buttons with descriptive tooltips
- Layer count display in footer ("Layers: X / 20")
- Active layer indicator chip in title bar

### Version 3.2 (December 2024) - Phase 3.2 Complete
- Eraser tool removes intersecting strokes
- Phase 2 marked complete

---

## Implementation Checklist

### Phase 3.1: Core Layer Infrastructure ✅ COMPLETE
- [x] Add `layer` and `layerId` properties to object creation
- [x] Add `LayerInfo` class to `ImageEditorState.cs`
- [x] Add `LayerType` enum
- [x] Track `activeLayerId` in JS state
- [x] Assign active layer to new drawings
- [x] Create default layers (Base Image, Drawing Layer)
- [x] Layer-aware erasing
- [x] Select tool implementation

### Phase 3.5: State Preservation ✅ COMPLETE
- [x] Save button (preserve layers, original base)
- [x] Apply button (flatten, reset state)
- [x] Separate BaseImageData from output
- [x] CanvasJson storage and restoration
- [x] Tool state sync on reopen
- [x] Layer state in serialization

### Phase 3.2: Image Import ✅ COMPLETE
- [x] Implement drag-drop handler on canvas
- [x] Add import button to toolbar
- [x] Implement clipboard paste (Ctrl+V)
- [x] Scale imported images to fit canvas
- [x] Assign imports to active layer
- [x] Persist imported images in state
- [x] Exclusive clipboard handling when editor open

### Phase 3.3: Layer Panel UI ✅ COMPLETE
- [x] Create `LayerPanel.razor` component
- [x] Implement collapsible sidebar
- [x] Layer list with visibility/lock toggles
- [x] Double-click to rename layer
- [x] Add/delete layer buttons
- [x] Layer reordering (up/down buttons)
- [x] Opacity slider for selected layer
- [x] Integrate with ImageEditorModal
- [x] Event handlers for all layer operations
- [x] Layer lock enforcement on canvas objects
- [x] State persistence for layers

### Phase 3.4: Object Management
- [x] Select tool (V)
- [x] Delete key handler
- [ ] Copy/paste (Ctrl+C/V)
- [ ] Duplicate (Ctrl+D)
- [ ] Flip horizontal/vertical

### Phase 3.6: Integration ✅ COMPLETE
- [x] Layer limit enforcement (20)
- [x] Import size limit (4096px)
- [x] Exclusive clipboard handling
- [x] Auto-reset state on input image change

---

## File Reference

### Core Files
| File | Purpose |
|------|---------|
| `Components/ImageEditor/ImageEditorModal.razor` | Main Blazor component |
| `Components/ImageEditor/ImageEditorModal.razor.css` | Scoped CSS styles |
| `Components/ImageEditor/LayerPanel.razor` | Layer management sidebar |
| `Components/ImageEditor/LayerPanel.razor.css` | Layer panel styles |
| `wwwroot/js/ImageEditor/ImageEditor.js` | Main entry point, initialization |
| `wwwroot/js/ImageEditor/ImageEditor.layers.js` | Layer management mixin |
| `wwwroot/js/ImageEditor/ImageEditor.canvas.js` | Canvas operations mixin |
| `wwwroot/js/ImageEditor/ImageEditor.tools.js` | Tool handling mixin |
| `wwwroot/js/ImageEditor/ImageEditor.history.js` | Undo/redo and state serialization |
| `wwwroot/js/ImageEditor/ImageEditor.export.js` | Export and import functionality |
| `wwwroot/js/ImageEditor/ImageEditor.events.js` | Event handlers mixin |
| `wwwroot/js/ImageEditor/ImageEditor.callbacks.js` | .NET interop callbacks |
| `wwwroot/js/ImageEditor/ImageEditor.utils.js` | Utility functions and constants |
| `Models/ImageEditorState.cs` | C# state model with LayerInfo class |
| `Services/ManagerService.cs` | ImageEditorState storage and auto-reset |

### Key Changes in 3.6
| File | Changes |
|------|---------|
| `ImageEditor.layers.js` | Added `_updateLayerLocked()` method, updated `updateLayer()` to call it |
| `ImageEditor.history.js` | Added layer state to `getState()`, restore in `loadState()`, apply locked state in `_finalizeStateRestore()` |
| `LayerPanel.razor` | Added local opacity state to prevent feedback loops, cleaned up debug code |
