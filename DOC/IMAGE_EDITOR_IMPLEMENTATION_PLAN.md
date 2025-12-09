# Image Editor Implementation Plan

> **Document Version:** 3.3  
> **Created:** December 2024  
> **Last Updated:** December 2024  
> **Status:** Phase 3.2 Complete  
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
| **3.3** | Layer Panel UI | 🔴 Not Started |
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
| Layer count display | Shows "Layers: X / 20" in footer | ✅ |
| Active layer indicator | Chip in title bar shows current layer | ✅ |

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
| Image persistence | Imported images restored on editor reopen | ✅ |
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

**Blazor Integration:**
- `MudFileUpload` component in toolbar for file picker
- `HandleFileImport(IBrowserFile)` method converts to data URL
- `OnImageImported` JSInvokable callback for import notification
- 10MB file size limit for uploads

**Import Behavior:**
- Imported images centered on canvas
- Auto-scaled to 80% of canvas size if too large
- Assigned to active layer
- Immediately selectable with Select tool (V)
- Transform handles (resize, rotate) available
- Persisted in canvas state for restoration

---

### Phase 3.3: Layer Panel UI 🔴 NOT STARTED

#### Planned Features

| Item | Description | Complexity |
|------|-------------|------------|
| Panel component | Collapsible right sidebar | 🟡 Medium |
| Layer list | Display layers with icons | 🟢 Low |
| Visibility toggle | Eye icon to show/hide layer | 🟢 Low |
| Lock toggle | Lock icon to prevent edits | 🟢 Low |
| Layer rename | Double-click to edit name | 🟢 Low |
| Layer reorder | Drag or up/down buttons | 🟡 Medium |
| Opacity slider | Per-layer opacity control | 🟡 Medium |
| Add/Delete buttons | Create/remove layers | 🟢 Low |

---

### Phase 3.4: Object Management 🟡 PARTIAL

#### Implemented

| Item | Status |
|------|--------|
| Select tool (V) | ✅ |
| Delete selected (Delete/Backspace) | ✅ |
| Object selection in Select mode | ✅ |
| Respect layer lock on delete | ✅ |

#### Remaining

| Item | Status |
|------|--------|
| Copy (Ctrl+C) | 🔴 |
| Paste (Ctrl+V) | 🔴 |
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

### Version 3.3 (December 2024) - Bug Fixes & Polish

#### Bug Fixes
- **Clipboard paste exclusive**: Paste events now only target the editor when open, preventing double-paste to both editor and main I2I page
  - Uses capture phase (`addEventListener(..., true)`) to intercept before other handlers
  - Calls `stopImmediatePropagation()` to prevent event bubbling
- **Image persistence**: Imported images now properly restored when reopening editor
  - Updated `_restoreObjectsDirectly()` to handle both `path` and `image` object types
  - Images restored with all transform properties (position, scale, rotation)
- **Syntax error fix**: Fixed missing parenthesis in `_addRestoredObjects()` that broke the canvas
- **Save vs Apply state preservation**: Fixed issue where Save was acting like Apply (flattening and losing state)
  - Root cause: Setting `Img2ImgInputImage` from editor output triggered auto-reset of editor state
  - Solution: Added `SetImg2ImgInputImage(value, resetEditorState)` method to differentiate user actions from editor output
  - `HandleImageDataChanged`: User loading new image → reset editor state
  - `HandleEditorApply`: Editor output → preserve editor state

#### State Management
- **Explicit reset control**: Editor state only resets when user manually changes input image
  - Removed auto-reset from `Img2ImgInputImage` setter
  - Added `SetImg2ImgInputImage()` method with explicit `resetEditorState` parameter
  - `ResetImageEditorState()` available for manual resets

#### CSS Fixes
- **Import button alignment**: Fixed `MudFileUpload` button alignment using `::deep` selector
- **ButtonTemplate fix**: Changed from `ActivatorContent` to `ButtonTemplate` for proper MudBlazor integration

### Version 3.2 (December 2024) - Phase 3.2 Complete

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

### Version 3.0 (December 2024) - Phase 3 Planning
- Comprehensive Phase 3 specification
- Technical decisions documented

### Version 2.3 (December 2024) - Phase 2 Complete
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

### Phase 3.2: Image Import ✅ COMPLETE
- [x] Implement drag-drop handler on canvas
- [x] Add import button to toolbar
- [x] Implement clipboard paste (Ctrl+V)
- [x] Scale imported images to fit canvas
- [x] Assign imports to active layer
- [x] Persist imported images in state
- [x] Exclusive clipboard handling when editor open

### Phase 3.3: Layer Panel UI
- [ ] Create `LayerPanel.razor` component
- [ ] Implement collapsible sidebar
- [ ] Layer list with visibility/lock toggles
- [ ] Double-click to rename layer
- [ ] Add/delete layer buttons
- [ ] Layer reordering (drag or buttons)
- [ ] Opacity slider for selected layer

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
| `wwwroot/js/ImageEditor.js` | Fabric.js integration with layer support |
| `Models/ImageEditorState.cs` | C# state model with LayerInfo class |
| `Services/ManagerService.cs` | ImageEditorState storage and auto-reset |

### Key Changes in 3.3
| File | Changes |
|------|---------|
| `ImageEditor.js` | Fixed paste handler (capture phase), image restoration in `_restoreObjectsDirectly()`, syntax fix |
| `ManagerService.cs` | Added `ResetImageEditorState()`, auto-reset in `Img2ImgInputImage` and `CanvasImageData` setters |
| `ImageEditorModal.razor` | Fixed `MudFileUpload` to use `ButtonTemplate` |
| `ImageEditorModal.razor.css` | Added `::deep` for import button alignment |

---

## Testing Notes

### Save/Apply Workflow Verification

1. **Blank Canvas + Save**
   - Create blank canvas, draw strokes
   - Click Save → Strokes visible in output
   - Reopen → Blank canvas with editable strokes on top ✅

2. **Blank Canvas + Apply**
   - Create blank canvas, draw strokes
   - Click Apply → Strokes visible in output
   - Reopen → Flattened image as new base, no editable strokes ✅

3. **Image + Save**
   - Load image, draw strokes
   - Click Save → Original + strokes in output
   - Reopen → Original image with editable strokes on top ✅

4. **Image + Apply**
   - Load image, draw strokes
   - Click Apply → Flattened output
   - Reopen → Flattened image as new base ✅

5. **Tool State Restoration**
   - Select tool, Save, Reopen → Select tool still active ✅

### Image Import Verification

6. **Drag & Drop**
   - Drag image file onto canvas → Image imported, centered ✅

7. **File Picker**
   - Click import button → File dialog opens → Image imported ✅

8. **Clipboard Paste**
   - Copy image to clipboard, Ctrl+V in editor → Image imported ✅
   - Paste does NOT affect main I2I page when editor is open ✅

9. **Import Persistence**
   - Import image → Save → Reopen → Imported image restored ✅

10. **State Reset on Input Change**
    - Edit image → Close editor → Change input image → Reopen → Fresh editor state ✅

---
